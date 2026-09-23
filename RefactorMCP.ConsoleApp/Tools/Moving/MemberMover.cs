using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Simplification;
using ModelContextProtocol;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

/// <summary>
/// Moves a method, field or property to another type. An instance member
/// moves through a field, property or parameter of the target type (the
/// "via"), which becomes <c>this</c> in its new home; a static member moves to
/// a named type. Uses are rewritten across the solution, or, for a method, a
/// delegating stub can stay behind.
/// </summary>
internal sealed class MemberMover
{
    private static readonly SyntaxAnnotation Removed = new(nameof(MemberMover) + "." + nameof(Removed));

    private readonly Solution _solution;
    private readonly ISymbol _member;
    private readonly INamedTypeSymbol _source;
    private readonly SyntaxEdits _edits = new();
    private readonly HashSet<ISymbol> _raised = new(SymbolEqualityComparer.Default);
    private readonly HashSet<string> _extensionNamespaces = new(StringComparer.Ordinal);

    private INamedTypeSymbol _target = null!;
    private ISymbol? _via;
    private bool _needsSource;
    private string _sourceParameter = "";

    private MemberMover(Solution solution, ISymbol member)
    {
        _solution = solution;
        _member = member;
        _source = member.ContainingType;
    }

    private bool IsMethod => _member is IMethodSymbol;

    private IMethodSymbol Method => (IMethodSymbol)_member;

    public static async Task<Solution> MoveAsync(
        Solution solution,
        ISymbol member,
        string? via,
        string? targetType,
        bool keepStub,
        string? targetFilePath,
        string? kind,
        CancellationToken cancellationToken)
    {
        if (member is not (IMethodSymbol { MethodKind: MethodKind.Ordinary } or IFieldSymbol or IPropertySymbol { IsIndexer: false }))
            throw new McpException($"Error: {member.Name} is not a method, field or property");
        EnsureKind(member, kind);

        var mover = new MemberMover(solution, member);
        return await mover.MoveAsync(via, targetType, keepStub && member is IMethodSymbol, targetFilePath, cancellationToken);
    }

    private async Task<Solution> MoveAsync(string? via, string? targetType, bool keepStub, string? targetFilePath, CancellationToken cancellationToken)
    {
        if (_member.IsVirtual || _member.IsOverride || _member.IsAbstract || ImplementsInterface())
            throw new McpException($"Error: {_member.Name} is virtual, abstract, an override or an interface implementation; callers rely on dispatch through the instance");

        var solution = _solution;
        if (_member.IsStatic)
            solution = await ResolveStaticTargetAsync(via, targetType, targetFilePath, cancellationToken);
        else
            ResolveVia(via, targetType);

        var declaration = await DeclarationAsync(cancellationToken);
        var document = solution.GetDocument(declaration.SyntaxTree)!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;

        var moved = BuildMovedMember(declaration, model);
        EnsureNoConflict();

        var targetDeclaration = (TypeDeclarationSyntax)await _target.DeclaringSyntaxReferences
            .OrderBy(r => r.SyntaxTree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal))
            .First()
            .GetSyntaxAsync(cancellationToken);
        EnsureTargetSeesSource(solution, document.Project, solution.GetDocument(targetDeclaration.SyntaxTree)!.Project);
        _edits.Replace(solution, targetDeclaration, rewritten => MemberLayout.Append((TypeDeclarationSyntax)rewritten, moved));

        if (keepStub)
            _edits.Replace(solution, declaration, _ => Stub((MethodDeclarationSyntax)declaration));
        else
        {
            await RewriteReferencesAsync(solution, declaration, cancellationToken);
            RemoveDeclaration(solution, declaration);
        }

        await RaiseAccessibilityAsync(solution, cancellationToken);

        var updated = await _edits.ApplyAsync(solution, cancellationToken);
        updated = await AddExtensionNamespacesAsync(updated, targetDeclaration.SyntaxTree, solution, cancellationToken);
        return await MovingSupport.RemoveNewlyUnnecessaryUsingsAsync(solution, updated, cancellationToken);
    }

    /// <summary>Refuses a member of another kind than the caller expected, when it said.</summary>
    private static void EnsureKind(ISymbol member, string? kind)
    {
        switch (kind)
        {
            case null:
                return;
            case "instance-method" when member is IMethodSymbol { IsStatic: true }:
                throw new McpException($"Error: {member.Name} is static; move it as a static method, to a named type");
            case "instance-method" when member is not IMethodSymbol:
            case "static-method" when member is not IMethodSymbol:
                throw new McpException($"Error: {member.Name} is not a method");
            case "static-method" when !member.IsStatic:
                throw new McpException($"Error: {member.Name} is an instance method; move it through a field, property or parameter");
            case "field" when member is not IFieldSymbol:
                throw new McpException($"Error: {member.Name} is not a field");
            case "property" when member is not IPropertySymbol:
                throw new McpException($"Error: {member.Name} is not a property");
            case "instance-method" or "static-method" or "field" or "property":
                return;
            default:
                throw new McpException($"Error: kind must be instance-method, static-method, field or property, not '{kind}'");
        }
    }

    private bool ImplementsInterface() =>
        _source.AllInterfaces
            .SelectMany(i => i.GetMembers())
            .Any(m => SymbolEqualityComparer.Default.Equals(_source.FindImplementationForInterfaceMember(m), _member));

    /// <summary>
    /// For an instance member, the field, property or parameter to move
    /// through: named by <paramref name="via"/>, or the one member or parameter
    /// whose type is <paramref name="targetType"/>.
    /// </summary>
    private void ResolveVia(string? via, string? targetType)
    {
        var parameters = _member is IMethodSymbol method ? method.Parameters.Cast<ISymbol>() : Enumerable.Empty<ISymbol>();
        var members = AllMembers(_source)
            .Where(m => m is IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } or IPropertySymbol { IsStatic: false, IsIndexer: false })
            .Where(m => !SymbolEqualityComparer.Default.Equals(m, _member));

        if (via is not null)
        {
            _via = parameters.FirstOrDefault(p => p.Name == via) ?? members.FirstOrDefault(m => m.Name == via)
                ?? throw new McpException($"Error: {_source.Name}.{_member.Name} has no field, property or parameter named {via} to move through");
        }
        else if (targetType is not null)
        {
            var candidates = parameters.Concat(members).Where(s => TypeNamed(TypeOf(s), targetType)).ToList();
            _via = candidates.Count switch
            {
                0 => throw new McpException($"Error: {_member.Name} has no field, property or parameter of type {targetType} to move through"),
                1 => candidates[0],
                _ => throw new McpException($"Error: {targetType} is reachable through several members ({string.Join(", ", candidates.Select(c => c.Name))}); pass via to choose one"),
            };
        }
        else
        {
            throw new McpException("Error: Pass via, the field, property or parameter to move through, or the target type");
        }

        if (TypeOf(_via) is not INamedTypeSymbol target || !target.Locations.Any(l => l.IsInSource))
            throw new McpException($"Error: {_via.Name} is of type {TypeOf(_via).ToDisplayString()}, which the solution does not declare");
        if (target.TypeKind is not (TypeKind.Class or TypeKind.Struct) || (target.TypeKind == TypeKind.Struct && !IsMethod))
            throw new McpException($"Error: {target.Name} is not a class{(IsMethod ? " or struct" : "")}, so {_member.Name} cannot move into it");
        if (SymbolEqualityComparer.Default.Equals(target.OriginalDefinition, _source.OriginalDefinition))
            throw new McpException($"Error: {_via.Name} is of type {_source.Name} itself; {_member.Name} is already there");
        if (!SymbolEqualityComparer.Default.Equals(target, target.OriginalDefinition))
            throw new McpException($"Error: {_via.Name} is of the constructed generic type {target.ToDisplayString()}; move into its definition by hand");

        _target = target;
    }

    private async Task<Solution> ResolveStaticTargetAsync(string? via, string? targetType, string? targetFilePath, CancellationToken cancellationToken)
    {
        if (via is not null)
            throw new McpException($"Error: {_member.Name} is static; name the target type instead of a member to move through");
        if (targetType is null)
            throw new McpException($"Error: Name the type to move {_member.Name} to");

        var found = await FindTypesAsync(targetType, cancellationToken);
        if (found.Count > 1)
            throw new McpException($"Error: Several types are named {targetType}; qualify it with its namespace");
        if (found.Count == 1)
        {
            var target = found[0];
            if (!target.Locations.Any(l => l.IsInSource))
                throw new McpException($"Error: {target.ToDisplayString()} is a type the solution does not declare");
            if (SymbolEqualityComparer.Default.Equals(target, _source))
                throw new McpException($"Error: {_member.Name} is already in {_source.Name}");
            if (target.TypeKind is not (TypeKind.Class or TypeKind.Struct))
                throw new McpException($"Error: {target.Name} is not a class or struct, so {_member.Name} cannot move into it");
            _target = target;
            return _solution;
        }

        if (!SyntaxFacts.IsValidIdentifier(targetType))
            throw new McpException($"Error: No type named {targetType} exists, and it is not a name a new type can take");

        return await CreateTargetTypeAsync(targetType, targetFilePath, cancellationToken);
    }

    private async Task<List<INamedTypeSymbol>> FindTypesAsync(string name, CancellationToken cancellationToken)
    {
        var found = new List<INamedTypeSymbol>();
        foreach (var project in _solution.Projects)
        {
            var compilation = (await project.GetCompilationAsync(cancellationToken))!;
            if (name.Contains('.'))
            {
                if (compilation.GetTypeByMetadataName(name) is { } byName)
                    found.Add(byName);
                continue;
            }

            var declared = await SymbolFinder.FindDeclarationsAsync(project, name, ignoreCase: false, SymbolFilter.Type, cancellationToken);
            found.AddRange(declared.OfType<INamedTypeSymbol>());
        }

        return found.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
    }

    /// <summary>
    /// A static class for the member, in a new file beside the source type's
    /// file, in the same namespace, as accessible as the source type.
    /// </summary>
    private async Task<Solution> CreateTargetTypeAsync(string name, string? targetFilePath, CancellationToken cancellationToken)
    {
        var sourceDeclaration = await _source.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var sourceDocument = _solution.GetDocument(sourceDeclaration.SyntaxTree)!;
        var path = targetFilePath ?? Path.Combine(Path.GetDirectoryName(sourceDocument.FilePath!)!, $"{name}.cs");
        if (File.Exists(path) || RefactoringHelpers.GetDocumentByPath(_solution, path) is not null)
            throw new McpException($"Error: File {path} already exists, and it does not declare {name}");

        var accessibility = _source.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        var header = $"{accessibility} static class {name}\n{{\n}}\n";
        var text = sourceDeclaration.Parent switch
        {
            FileScopedNamespaceDeclarationSyntax ns => $"namespace {ns.Name};\n\n{header}",
            NamespaceDeclarationSyntax ns => $"namespace {ns.Name}\n{{\n    {header.TrimEnd().Replace("\n", "\n    ")}\n}}\n",
            _ => header,
        };

        var created = MovingSupport.AddDocument(sourceDocument.Project, path, SyntaxFactory.ParseCompilationUnit(text));
        var compilation = (await created.Project.GetCompilationAsync(cancellationToken))!;
        var tree = (await created.GetSyntaxTreeAsync(cancellationToken))!;
        var declaration = (await tree.GetRootAsync(cancellationToken)).DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        _target = compilation.GetSemanticModel(tree).GetDeclaredSymbol(declaration, cancellationToken)!;
        return created.Project.Solution;
    }

    private async Task<SyntaxNode> DeclarationAsync(CancellationToken cancellationToken)
    {
        var reference = _member.DeclaringSyntaxReferences.FirstOrDefault()
            ?? throw new McpException($"Error: {_member.Name} has no declaration in source");
        var node = await reference.GetSyntaxAsync(cancellationToken);
        if (_member.DeclaringSyntaxReferences.Length > 1)
            throw new McpException($"Error: {_member.Name} is a partial method; move its parts together by hand");
        return node;
    }

    /// <summary>
    /// The member as it will read in the target: uses of the via become
    /// <c>this</c>, other members of the source are reached through a source
    /// parameter or the source type, and names are qualified so the target
    /// file can import what they need.
    /// </summary>
    private MemberDeclarationSyntax BuildMovedMember(SyntaxNode declaration, SemanticModel model)
    {
        var member = declaration is VariableDeclaratorSyntax variable
            ? (MemberDeclarationSyntax)variable.Parent!.Parent!
            : (MemberDeclarationSyntax)declaration;

        if (IsMethod)
        {
            _sourceParameter = MovingSupport.CamelCase(_source.Name);
            var taken = member.DescendantNodes().OfType<ParameterSyntax>().Select(p => p.Identifier.ValueText)
                .Concat(member.DescendantNodes().OfType<VariableDeclaratorSyntax>().Select(v => v.Identifier.ValueText));
            if (taken.Contains(_sourceParameter))
                throw new McpException($"Error: {_member.Name} already has a parameter or local named {_sourceParameter}");
        }

        var rewrites = new Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>();
        var scope = declaration is VariableDeclaratorSyntax declarator
            ? new SyntaxNode[] { ((VariableDeclarationSyntax)declarator.Parent!).Type, declarator }
            : new[] { declaration };
        foreach (var node in scope.SelectMany(n => n.DescendantNodesAndSelf()))
            CollectRewrite(node, model, rewrites);

        if (_needsSource && !IsMethod)
            throw new McpException($"Error: {_member.Name} uses other members of {_source.Name}, which a {(_member is IFieldSymbol ? "field" : "property")} cannot be given as a parameter; move them first");

        var rewritten = member.ReplaceNodes(rewrites.Keys.Where(k => member.Contains(k)), (original, current) => rewrites[original](current));
        if (declaration is VariableDeclaratorSyntax single && ((VariableDeclarationSyntax)single.Parent!).Variables.Count > 1)
        {
            var field = (FieldDeclarationSyntax)rewritten;
            var kept = field.Declaration.Variables.First(v => v.Identifier.ValueText == single.Identifier.ValueText);
            rewritten = field
                .WithDeclaration(field.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(kept.WithoutTrivia())))
                .WithLeadingTrivia(SyntaxFactory.ElasticMarker);
        }

        rewritten = rewritten.WithModifiers(RaisedForTarget(rewritten.Modifiers));
        if (rewritten is MethodDeclarationSyntax method)
            rewritten = WithMovedParameters(method);

        return rewritten;
    }

    private void CollectRewrite(SyntaxNode node, SemanticModel model, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>> rewrites)
    {
        switch (node)
        {
            case BaseExpressionSyntax:
                throw new McpException($"Error: {_member.Name} calls through base, which it cannot do from {_target.Name}");

            case ThisExpressionSyntax self when IsViaUse(self.Parent, model):
                return;

            case ThisExpressionSyntax self:
                _needsSource = true;
                rewrites[self] = current => SyntaxFactory.IdentifierName(_sourceParameter).WithTriviaFrom(current);
                return;

            case SimpleNameSyntax name when _via is not null && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, _via):
                RewriteViaUse(name, model, rewrites);
                return;

            case SimpleNameSyntax name when MemberReferences.IsImplicitInstanceMember(name, model, _source, out var used):
                if (SymbolEqualityComparer.Default.Equals(used, _member))
                    throw new McpException($"Error: {_member.Name} calls itself; move it by hand");
                _needsSource = true;
                NoteAccess(used);
                rewrites[name] = current => SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(_sourceParameter),
                        ((SimpleNameSyntax)current).WithoutTrivia())
                    .WithTriviaFrom(current);
                return;

            case SimpleNameSyntax name:
                RewriteOtherName(name, model, rewrites);
                return;
        }
    }

    /// <summary><c>via.X</c> becomes <c>X</c> (or <c>this.X</c> where hidden); the via itself becomes <c>this</c>.</summary>
    private void RewriteViaUse(SimpleNameSyntax name, SemanticModel model, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>> rewrites)
    {
        ExpressionSyntax use = name;
        if (name.Parent is MemberAccessExpressionSyntax qualified && qualified.Name == name)
        {
            if (qualified.Expression is not ThisExpressionSyntax)
            {
                NoteAccess(_via!);
                return;
            }

            use = qualified;
        }

        if (MemberReferences.IsWrittenTo(use))
            throw new McpException($"Error: {_member.Name} assigns {_via!.Name}, the member it would move through");

        if (use.Parent is MemberAccessExpressionSyntax access && access.Expression == use)
        {
            var bare = MemberReferences.CanDropQualifier(access, model, _target);
            rewrites[access] = current =>
            {
                var rewritten = (MemberAccessExpressionSyntax)current;
                return bare
                    ? rewritten.Name.WithTriviaFrom(rewritten)
                    : rewritten.WithExpression(SyntaxFactory.ThisExpression().WithTriviaFrom(rewritten.Expression));
            };
        }
        else
        {
            rewrites[use] = current => SyntaxFactory.ThisExpression().WithTriviaFrom(current);
        }
    }

    private bool IsViaUse(SyntaxNode? parent, SemanticModel model) =>
        _via is not null
        && parent is MemberAccessExpressionSyntax access
        && access.Expression is ThisExpressionSyntax
        && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(access.Name).Symbol, _via);

    /// <summary>
    /// Names that are not instance members of the source: static members of
    /// the source are qualified by their type, types are qualified so the
    /// target can import them, and private members reached any other way are
    /// raised so the target can still reach them.
    /// </summary>
    private void RewriteOtherName(SimpleNameSyntax name, SemanticModel model, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>> rewrites)
    {
        var info = model.GetSymbolInfo(name);
        var symbol = info.Symbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);
        if (symbol is null)
            return;

        if (symbol is IMethodSymbol { ReducedFrom: not null } extension)
            _extensionNamespaces.Add(extension.ContainingType.ContainingNamespace.ToDisplayString());

        if (symbol.ContainingType is not null && MemberReferences.InheritsFrom(_source, symbol.ContainingType))
            NoteAccess(symbol);

        var qualified = name.Parent is MemberAccessExpressionSyntax access && access.Name == name
            || name.Parent is QualifiedNameSyntax q && q.Right == name
            || name.Parent is AliasQualifiedNameSyntax
            || name.Parent is MemberBindingExpressionSyntax
            || name.Parent is NameColonSyntax or NameEqualsSyntax;
        if (qualified)
            return;

        if (symbol is IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol && symbol.IsStatic
            && symbol.ContainingType is not null
            && MemberReferences.InheritsFrom(_source, symbol.ContainingType)
            && !SymbolEqualityComparer.Default.Equals(symbol, _member))
        {
            var owner = symbol.ContainingType;
            rewrites[name] = current => SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    MovingSupport.QualifiedType(owner),
                    ((SimpleNameSyntax)current).WithoutTrivia())
                .WithTriviaFrom(current);
        }
        else if (symbol is INamedTypeSymbol type
            && type.TypeKind != TypeKind.TypeParameter
            && type.SpecialType == SpecialType.None
            && name.Identifier.ValueText == type.Name
            && !name.IsVar)
        {
            rewrites[name] = current => MovingSupport.QualifiedType(type).WithTriviaFrom(current);
        }
    }

    /// <summary>Records a member of the source the moved code reaches, to raise it from private.</summary>
    private void NoteAccess(ISymbol symbol)
    {
        if (SymbolEqualityComparer.Default.Equals(symbol, _member))
            return;
        if (symbol.DeclaredAccessibility is Accessibility.Protected or Accessibility.ProtectedAndInternal)
            throw new McpException($"Error: {_member.Name} uses the protected member {symbol.Name}, which {_target.Name} cannot reach");
        if (symbol.DeclaredAccessibility == Accessibility.Private && symbol.Locations.Any(l => l.IsInSource))
            _raised.Add(symbol.OriginalDefinition);
    }

    /// <summary>
    /// A moved member at least internal, so the code it left can still reach
    /// it. Protected access means nothing outside its type, so it becomes
    /// internal too.
    /// </summary>
    private static SyntaxTokenList RaisedForTarget(SyntaxTokenList modifiers)
    {
        if (!modifiers.Any(SyntaxKind.ProtectedKeyword))
            return MovingSupport.RaisePrivateToInternal(modifiers);

        static bool IsAccessibility(SyntaxToken m) =>
            m.Kind() is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword or SyntaxKind.ProtectedKeyword or SyntaxKind.PrivateKeyword;

        var accessibility = modifiers.Where(IsAccessibility).ToList();
        var index = modifiers.IndexOf(accessibility[0]);
        var others = SyntaxFactory.TokenList(modifiers.Where(m => !IsAccessibility(m)));
        var keyword = SyntaxFactory.Token(accessibility[0].LeadingTrivia, SyntaxKind.InternalKeyword, accessibility[^1].TrailingTrivia);
        return others.Insert(index, keyword);
    }

    /// <summary>The via parameter goes, and the source instance comes first when the body needs it.</summary>
    private MethodDeclarationSyntax WithMovedParameters(MethodDeclarationSyntax method)
    {
        var parameters = method.ParameterList.Parameters;
        if (_via is IParameterSymbol viaParameter)
            parameters = parameters.RemoveAt(viaParameter.Ordinal);
        if (_needsSource)
        {
            parameters = SyntaxEdits.Prepend(parameters, new[]
            {
                SyntaxFactory.Parameter(SyntaxFactory.Identifier(_sourceParameter)).WithType(MovingSupport.QualifiedType(_source).WithTrailingTrivia(SyntaxFactory.Space)),
            });
        }

        return method.WithParameterList(method.ParameterList.WithParameters(parameters));
    }

    private void EnsureNoConflict()
    {
        foreach (var existing in _target.GetMembers(_member.Name))
        {
            if (!IsMethod || existing is not IMethodSymbol other)
                throw new McpException($"Error: {_target.Name} already has a member named {_member.Name}");

            var types = MovedParameterTypes();
            if (other.Parameters.Length == types.Count
                && other.Parameters.Select(p => p.Type).Zip(types).All(pair => SymbolEqualityComparer.Default.Equals(pair.First, pair.Second)))
            {
                throw new McpException($"Error: {_target.Name} already has a method {_member.Name} with the same parameters");
            }
        }
    }

    /// <summary>
    /// A moved member that takes the source instance, or reaches the source
    /// type, needs the target's project to see the source's.
    /// </summary>
    private void EnsureTargetSeesSource(Solution solution, Project source, Project target)
    {
        if (source.Id == target.Id || !(_needsSource || _member.IsStatic))
            return;

        var seen = solution.GetProjectDependencyGraph().GetProjectsThatThisProjectTransitivelyDependsOn(target.Id);
        if (!seen.Contains(source.Id))
            throw new McpException($"Error: {_target.Name} is in project {target.Name}, which cannot see {_source.Name} in project {source.Name}");
    }

    private List<ITypeSymbol> MovedParameterTypes()
    {
        var types = Method.Parameters
            .Where(p => !SymbolEqualityComparer.Default.Equals(p, _via))
            .Select(p => p.Type)
            .ToList();
        if (_needsSource)
            types.Insert(0, _source);
        return types;
    }

    /// <summary>
    /// The original method with a body that calls the moved one, keeping its
    /// signature and documentation but not its other comments.
    /// </summary>
    private MethodDeclarationSyntax Stub(MethodDeclarationSyntax original)
    {
        var arguments = new List<ArgumentSyntax>();
        if (_needsSource)
            arguments.Add(SyntaxFactory.Argument(SyntaxFactory.ThisExpression()));
        foreach (var parameter in Method.Parameters.Where(p => !SymbolEqualityComparer.Default.Equals(p, _via)))
            arguments.Add(ArgumentFor(parameter));

        SimpleNameSyntax name = Method.IsGenericMethod
            ? SyntaxFactory.GenericName(Method.Name).WithTypeArgumentList(SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList<TypeSyntax>(Method.TypeParameters.Select(t => SyntaxFactory.IdentifierName(t.Name)))))
            : SyntaxFactory.IdentifierName(Method.Name);
        ExpressionSyntax receiver = _via is null
            ? MovingSupport.QualifiedType(_target)
            : SyntaxFactory.IdentifierName(_via.Name);
        var call = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, name),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));

        var modifiers = SyntaxFactory.TokenList(original.Modifiers.Where(m => !m.IsKind(SyntaxKind.AsyncKeyword)));
        var stub = original.WithModifiers(modifiers).WithLeadingTrivia(DocumentationOnly(original.GetLeadingTrivia()));
        StatementSyntax statement = Method.ReturnsVoid
            ? SyntaxFactory.ExpressionStatement(call)
            : SyntaxFactory.ReturnStatement(call);
        stub = original.ExpressionBody is not null
            ? stub.WithExpressionBody(SyntaxFactory.ArrowExpressionClause(call))
            : stub.WithBody(SyntaxFactory.Block(statement)).WithExpressionBody(null).WithSemicolonToken(default);
        return stub.WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static ArgumentSyntax ArgumentFor(IParameterSymbol parameter)
    {
        var argument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameter.Name));
        return parameter.RefKind switch
        {
            RefKind.Ref => argument.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword)),
            RefKind.Out => argument.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.OutKeyword)),
            RefKind.In => argument.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.InKeyword)),
            _ => argument,
        };
    }

    /// <summary>The whitespace and documentation comments of leading trivia, without ordinary comments.</summary>
    private static SyntaxTriviaList DocumentationOnly(SyntaxTriviaList trivia)
    {
        var kept = new List<SyntaxTrivia>();
        var lineHasComment = false;
        var line = new List<SyntaxTrivia>();
        foreach (var piece in trivia)
        {
            line.Add(piece);
            if (piece.IsKind(SyntaxKind.SingleLineCommentTrivia) || piece.IsKind(SyntaxKind.MultiLineCommentTrivia))
                lineHasComment = true;
            if (piece.IsKind(SyntaxKind.EndOfLineTrivia) || piece.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || piece.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                if (!lineHasComment)
                    kept.AddRange(line);
                line.Clear();
                lineHasComment = false;
            }
        }

        kept.AddRange(line);
        return SyntaxFactory.TriviaList(kept);
    }

    /// <summary>Rewrites every use of the member, which is about to be removed, to reach it in the target.</summary>
    private async Task RewriteReferencesAsync(Solution solution, SyntaxNode declaration, CancellationToken cancellationToken)
    {
        foreach (var reference in await MemberReferences.FindAsync(solution, _member, cancellationToken))
        {
            if (reference.Name.SyntaxTree == declaration.SyntaxTree && declaration.Span.Contains(reference.Name.Span))
                continue;

            var document = solution.GetDocument(reference.Name.SyntaxTree)!.Id;
            var file = Path.GetFileName(reference.Document.FilePath);
            if (reference.IsConditional)
                throw new McpException($"Error: {_member.Name} is used with null-conditional access (?.) in {file}; rewrite that use first");
            if (reference.Name.Parent is AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax initializer }
                && initializer.IsKind(SyntaxKind.ObjectInitializerExpression))
            {
                throw new McpException($"Error: An object initializer sets {_member.Name} in {file}, which cannot set it through another member");
            }

            if (_member.IsStatic)
            {
                _edits.Replace(document, reference.Callee, current => StaticAccess(reference.Name).WithTriviaFrom(current));
                continue;
            }

            if (IsMethod && reference.Invocation is null)
                throw new McpException($"Error: {_member.Name} is used as a method group in {file}; its signature changes, so keep a stub");

            if (_via is IParameterSymbol parameter)
            {
                RewriteCallThroughParameter(reference, parameter, document, cancellationToken);
                continue;
            }

            if (_member is IFieldSymbol { IsReadOnly: true } && MemberReferences.IsWrittenTo(reference.Name))
                throw new McpException($"Error: {_member.Name} is readonly and assigned in {file}, which it could not be through {_via!.Name}");

            if (!reference.Model.IsAccessible(reference.Name.SpanStart, _via!))
                throw new McpException($"Error: {_via!.Name} is not accessible where {_member.Name} is used in {file}");

            var receiver = reference.Receiver?.WithoutTrivia();
            var through = receiver is null
                ? (ExpressionSyntax)SyntaxFactory.IdentifierName(_via!.Name)
                : SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, SyntaxFactory.IdentifierName(_via!.Name));

            if (!IsMethod)
            {
                _edits.Replace(document, reference.Callee, current => SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression, through, reference.Name.WithoutTrivia())
                    .WithTriviaFrom(current));
                continue;
            }

            if (_needsSource && !reference.IsOnThis && !MemberReferences.IsSimple(reference.Receiver!))
                throw new McpException($"Error: A call to {_member.Name} is made on '{reference.Receiver}', which would be evaluated twice; store it in a local first");

            var instance = reference.IsOnThis ? (ExpressionSyntax)SyntaxFactory.ThisExpression() : receiver!;
            _edits.Replace(document, reference.Invocation!, current =>
            {
                var call = (InvocationExpressionSyntax)current;
                var callee = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, through, NameOf(call).WithoutTrivia());
                var arguments = _needsSource
                    ? SyntaxEdits.Prepend(call.ArgumentList.Arguments, new[] { SyntaxFactory.Argument(instance) })
                    : call.ArgumentList.Arguments;
                return call.WithExpression(callee.WithTriviaFrom(call.Expression)).WithArgumentList(call.ArgumentList.WithArguments(arguments));
            });
        }
    }

    /// <summary>
    /// The member named through the target type, reduced by the simplifier
    /// where the code allows. A generic name keeps its type arguments.
    /// </summary>
    private ExpressionSyntax StaticAccess(SimpleNameSyntax name)
    {
        var access = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            MovingSupport.QualifiedType(_target),
            name.WithoutTrivia());
        return name is IdentifierNameSyntax ? access.WithAdditionalAnnotations(Simplifier.Annotation) : access;
    }

    private void RewriteCallThroughParameter(MemberReference reference, IParameterSymbol parameter, DocumentId document, CancellationToken cancellationToken)
    {
        var invocation = reference.Invocation!;
        if (reference.Model.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation)
            throw new McpException($"Error: A call to {_member.Name} could not be analysed");

        var argument = operation.Arguments.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.Parameter?.OriginalDefinition, parameter.OriginalDefinition));
        if (argument?.Syntax is not ArgumentSyntax argumentSyntax)
            throw new McpException($"Error: A call to {_member.Name} does not pass {parameter.Name} explicitly");
        if (argumentSyntax.Expression.IsKind(SyntaxKind.NullLiteralExpression) || argumentSyntax.Expression.IsKind(SyntaxKind.DefaultLiteralExpression))
            throw new McpException($"Error: A call to {_member.Name} passes null for {parameter.Name}, which would then throw before the method runs");
        if (!reference.IsOnThis && !MemberReferences.IsSimple(reference.Receiver!))
            throw new McpException($"Error: A call to {_member.Name} is made on '{reference.Receiver}', which would no longer be evaluated; store it in a local first");

        var index = invocation.ArgumentList.Arguments.IndexOf(argumentSyntax);
        var instance = reference.IsOnThis ? (ExpressionSyntax)SyntaxFactory.ThisExpression() : reference.Receiver!.WithoutTrivia();
        _edits.Replace(document, invocation, current =>
        {
            var call = (InvocationExpressionSyntax)current;
            var receiver = ExtensionMethodConversions.Receiver(call.ArgumentList.Arguments[index].Expression);
            var arguments = call.ArgumentList.Arguments.RemoveAt(index);
            if (_needsSource)
                arguments = SyntaxEdits.Prepend(arguments, new[] { SyntaxFactory.Argument(instance) });
            var callee = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, NameOf(call).WithoutTrivia());
            return call.WithExpression(callee.WithTriviaFrom(call.Expression)).WithArgumentList(call.ArgumentList.WithArguments(arguments));
        });
    }

    private static SimpleNameSyntax NameOf(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name,
        SimpleNameSyntax name => name,
        _ => throw new McpException($"Error: Unexpected call form '{call.Expression}'"),
    };

    /// <summary>
    /// Marks the declaration, and removes marked members when their type is
    /// rewritten; a field declared alongside others loses only its variable.
    /// </summary>
    private void RemoveDeclaration(Solution solution, SyntaxNode declaration)
    {
        _edits.Replace(solution, declaration, current => current.WithAdditionalAnnotations(Removed));
        var type = declaration.Ancestors().OfType<TypeDeclarationSyntax>().First();
        _edits.Replace(solution, type, current =>
        {
            var rewritten = (TypeDeclarationSyntax)current;
            var marked = rewritten.GetAnnotatedNodes(Removed).Single();
            if (marked is VariableDeclaratorSyntax variable && variable.Parent is VariableDeclarationSyntax { Variables.Count: > 1 } variables)
                return rewritten.ReplaceNode(variables, variables.WithVariables(variables.Variables.Remove(variable)));

            var member = marked as MemberDeclarationSyntax ?? marked.FirstAncestorOrSelf<MemberDeclarationSyntax>()!;
            return rewritten.WithMembers(MemberLayout.Remove(rewritten.Members, member));
        });
    }

    /// <summary>Private members of the source the moved code still reaches become internal.</summary>
    private async Task RaiseAccessibilityAsync(Solution solution, CancellationToken cancellationToken)
    {
        foreach (var symbol in _raised)
        {
            foreach (var reference in symbol.DeclaringSyntaxReferences)
            {
                var node = await reference.GetSyntaxAsync(cancellationToken);
                var declaration = node is VariableDeclaratorSyntax
                    ? node.FirstAncestorOrSelf<MemberDeclarationSyntax>()!
                    : (MemberDeclarationSyntax)node;
                _edits.Replace(solution, declaration, current =>
                {
                    var member = (MemberDeclarationSyntax)current;
                    return member.WithModifiers(MovingSupport.RaisePrivateToInternal(member.Modifiers));
                });
            }
        }
    }

    private async Task<Solution> AddExtensionNamespacesAsync(Solution updated, SyntaxTree targetTree, Solution before, CancellationToken cancellationToken)
    {
        if (_extensionNamespaces.Count == 0)
            return updated;

        var id = before.GetDocument(targetTree)!.Id;
        var document = updated.GetDocument(id)!;
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
        var ns = _target.ContainingNamespace.ToDisplayString();
        var needed = _extensionNamespaces.Where(n => n != ns && !ns.StartsWith(n + ".", StringComparison.Ordinal));
        var withUsings = MovingSupport.AddUsings(root, needed);
        if (withUsings == root)
            return updated;

        var tidied = await Formatter.FormatAsync(document.WithSyntaxRoot(withUsings), Formatter.Annotation, cancellationToken: cancellationToken);
        return tidied.Project.Solution;
    }

    private static IEnumerable<ISymbol> AllMembers(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
                yield return member;
        }
    }

    private static ITypeSymbol TypeOf(ISymbol symbol) => symbol switch
    {
        IParameterSymbol p => p.Type,
        IFieldSymbol f => f.Type,
        IPropertySymbol p => p.Type,
        _ => throw new InvalidOperationException(),
    };

    private static bool TypeNamed(ITypeSymbol type, string name) =>
        type.Name == name || type.ToDisplayString() == name || type.OriginalDefinition.ToDisplayString() == name;
}
