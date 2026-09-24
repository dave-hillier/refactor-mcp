using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RefactorMCP.ConsoleApp.Tools.Composites;

[McpServerToolType]
public static class ReplaceMethodWithMethodObjectTool
{
    [McpServerTool, Description("Replace Method with Method Object: move a method's body into a new class whose fields hold the instance, " +
        "the parameters and the locals, and make the method create an instance for each call and run it. " +
        "Refuses, changing nothing, when the body cannot move.")]
    public static async Task<string> ReplaceMethodWithMethodObject(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method")] string methodName,
        [Description("Name of the new class")] string className,
        [Description("Name of the new class's method that runs the body (default Compute)")] string computeMethodName = "Compute",
        [Description("File for the new class (optional, defaults to <className>.cs beside the method's file)")] string? targetFilePath = null,
        [Description("Line of the method's declaration (1-based, optional), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var method = (IMethodSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, methodName, line, s => s is IMethodSymbol { MethodKind: MethodKind.Ordinary }, "method", cancellationToken);

        var extraction = new MethodObjectExtraction(solution, method, className, computeMethodName);
        var updated = await extraction.ExtractAsync(targetFilePath, cancellationToken);
        await SolutionEdits.EnsureCompilesAsync(solution, updated, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully replaced {methodName} with the method object {className}";
    }
}

/// <summary>
/// Builds the method object for one method: a class with a field for the
/// instance, each parameter and each local, a constructor taking the instance
/// and the parameters, and a method running the old body against the fields.
/// </summary>
internal sealed class MethodObjectExtraction
{
    private readonly Solution _solution;
    private readonly IMethodSymbol _method;
    private readonly INamedTypeSymbol _source;
    private readonly string _className;
    private readonly string _computeName;
    private readonly HashSet<ISymbol> _raised = new(SymbolEqualityComparer.Default);
    private readonly HashSet<string> _extensionNamespaces = new(StringComparer.Ordinal);
    private readonly HashSet<ILocalSymbol> _locals = new(SymbolEqualityComparer.Default);
    private readonly string _instanceField;
    private bool _needsInstance;

    public MethodObjectExtraction(Solution solution, IMethodSymbol method, string className, string computeName)
    {
        _solution = solution;
        _method = method;
        _source = method.ContainingType;
        _className = className;
        _computeName = computeName;
        _instanceField = "_" + MovingSupport.CamelCase(_source.Name).TrimStart('@');
    }

    private string InstanceParameter => MovingSupport.CamelCase(_source.Name);

    public async Task<Solution> ExtractAsync(string? targetFilePath, CancellationToken cancellationToken)
    {
        if (_method.PartialDefinitionPart is not null || _method.PartialImplementationPart is not null || _method.DeclaringSyntaxReferences.Length != 1)
            throw new McpException($"Error: {_method.Name} is a partial method, which has no single body to move");

        var declaration = (MethodDeclarationSyntax)await _method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var document = _solution.GetDocument(declaration.SyntaxTree)!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        SyntaxNode body = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody?.Expression
            ?? throw new McpException($"Error: {_method.Name} has no body to move");

        EnsureMovable(body);
        var path = targetFilePath ?? Path.Combine(Path.GetDirectoryName(document.FilePath!)!, _className + ".cs");
        await EnsureNewTypeIsFreeAsync(document.Project, path, cancellationToken);

        CollectLocals(declaration, body, model);
        var rewrittenBody = RewriteBody(body, model);
        EnsureNoNameConflicts(body, model);

        var edits = new SyntaxEdits();
        edits.Replace(document.Id, declaration, current => CallMethodObject((MethodDeclarationSyntax)current));
        foreach (var member in _raised)
            await RaiseAsync(edits, member, cancellationToken);
        var updated = await edits.ApplyAsync(_solution, cancellationToken);
        updated = await MovingSupport.RemoveNewlyUnnecessaryUsingsAsync(_solution, updated, cancellationToken);

        var type = BuildClass(declaration, model, rewrittenBody);
        var root = CompilationUnitFor(declaration, type);
        root = MovingSupport.AddUsings(root, _extensionNamespaces);
        var created = MovingSupport.AddDocument(updated.GetDocument(document.Id)!.Project, path, root);
        created = await Formatter.FormatAsync(created, cancellationToken: cancellationToken);
        created = await MovingSupport.TidyAsync(created, cancellationToken);

        // The file ends with a line break, as the old class's file does.
        var text = await created.GetTextAsync(cancellationToken);
        var newLine = TypeDeclarations.NewLine(declaration.SyntaxTree.GetRoot()).ToString();
        if (!text.ToString().EndsWith(newLine, StringComparison.Ordinal))
            created = created.WithText(text.WithChanges(new TextChange(new TextSpan(text.Length, 0), newLine)));
        return created.Project.Solution;
    }

    private void EnsureMovable(SyntaxNode body)
    {
        if (_method.TypeParameters.Length > 0)
            throw new McpException($"Error: {_method.Name} has type parameters, which the method object would have to declare");
        for (var type = _source; type is not null; type = type.ContainingType)
        {
            if (type.TypeParameters.Length > 0)
                throw new McpException($"Error: {type.Name} is generic, which the method object would have to be too");
        }

        if (!_method.IsStatic && _source.TypeKind != TypeKind.Class)
            throw new McpException($"Error: {_source.Name} is not a class, so the method object would work on a copy of it");

        var byReference = _method.Parameters.FirstOrDefault(p => p.RefKind != RefKind.None);
        if (byReference is not null)
            throw new McpException($"Error: {byReference.Name} is passed by reference, which a field cannot hold");

        if (body.DescendantNodesAndSelf().OfType<BaseExpressionSyntax>().Any())
            throw new McpException($"Error: {_method.Name} calls through base, which it cannot do from {_className}");
    }

    private async Task EnsureNewTypeIsFreeAsync(Project project, string path, CancellationToken cancellationToken)
    {
        var compilation = (await project.GetCompilationAsync(cancellationToken))!;
        var ns = _source.ContainingNamespace;
        if (ns.GetTypeMembers(_className).Any() || compilation.GetTypeByMetadataName(QualifiedName(ns, _className)) is not null)
            throw new McpException($"Error: A type named {QualifiedName(ns, _className)} already exists");
        if (File.Exists(path) || RefactoringHelpers.GetDocumentByPath(_solution, path) is not null)
            throw new McpException($"Error: File {path} already exists");
    }

    private static string QualifiedName(INamespaceSymbol ns, string name) =>
        ns.IsGlobalNamespace ? name : $"{ns.ToDisplayString()}.{name}";

    /// <summary>
    /// The locals that become fields: those declared by a plain declaration
    /// statement of the method itself, whose type can be written, and that no
    /// lambda or local function captures, since a captured local declared in
    /// a loop is a new variable each time round and a field would not be.
    /// </summary>
    private void CollectLocals(MethodDeclarationSyntax declaration, SyntaxNode body, SemanticModel model)
    {
        var captured = declaration.Body is null
            ? Array.Empty<ISymbol>()
            : model.AnalyzeDataFlow(declaration.Body)?.Captured.ToArray() ?? Array.Empty<ISymbol>();

        foreach (var statement in body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
        {
            if (statement.IsConst || statement.UsingKeyword.IsKind(SyntaxKind.UsingKeyword) || statement.Declaration.Type is RefTypeSyntax)
                continue;
            if (statement.Ancestors().TakeWhile(a => a != body).Any(a => a is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
                continue;

            var locals = statement.Declaration.Variables.Select(v => (ILocalSymbol)model.GetDeclaredSymbol(v)!).ToList();
            if (locals.All(l => !captured.Contains(l, SymbolEqualityComparer.Default) && CanBeField(l.Type)))
                _locals.UnionWith(locals);
        }

        var names = _locals.Select(l => l.Name).Concat(_method.Parameters.Select(p => p.Name)).ToList();
        var repeated = names.GroupBy(n => n).FirstOrDefault(g => g.Count() > 1);
        if (repeated is not null)
            throw new McpException($"Error: {_method.Name} declares '{repeated.Key}' more than once, which one field cannot stand for");
    }

    private static bool CanBeField(ITypeSymbol type) => type switch
    {
        { IsAnonymousType: true } => false,
        IArrayTypeSymbol array => CanBeField(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.All(CanBeField),
        IPointerTypeSymbol => false,
        _ => type.TypeKind != TypeKind.Error,
    };

    private static string FieldName(string name) => "_" + name.TrimStart('@');

    /// <summary>
    /// The body as the method object runs it: parameters and locals become
    /// fields, the instance's members are reached through the instance field,
    /// and the old class's static members and types are qualified.
    /// </summary>
    private SyntaxNode RewriteBody(SyntaxNode body, SemanticModel model)
    {
        var rewrites = new Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>();
        foreach (var node in body.DescendantNodesAndSelf())
        {
            switch (node)
            {
                case ThisExpressionSyntax:
                    _needsInstance = true;
                    rewrites[node] = current => IdentifierName(_instanceField).WithTriviaFrom(current);
                    break;

                case LocalDeclarationStatementSyntax statement
                    when statement.Declaration.Variables.Any(v => _locals.Contains(model.GetDeclaredSymbol(v)!)):
                    rewrites[node] = current => current.WithAdditionalAnnotations(ConvertedDeclaration);
                    break;

                case SimpleNameSyntax name:
                    RewriteName(name, model, rewrites);
                    break;
            }
        }

        var rewritten = body.ReplaceNodes(rewrites.Keys, (original, current) => rewrites[original](current));
        return ReplaceConvertedDeclarations(rewritten);
    }

    private static readonly SyntaxAnnotation ConvertedDeclaration = new(nameof(MethodObjectExtraction));

    private void RewriteName(SimpleNameSyntax name, SemanticModel model, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>> rewrites)
    {
        var info = model.GetSymbolInfo(name);
        var symbol = info.Symbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);
        if (symbol is null)
            return;

        if (symbol is IParameterSymbol parameter && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, _method)
            || symbol is ILocalSymbol local && _locals.Contains(local))
        {
            rewrites[name] = current => IdentifierName(FieldName(name.Identifier.ValueText)).WithTriviaFrom(current);
            return;
        }

        if (symbol is IMethodSymbol { ReducedFrom: not null } extension)
            _extensionNamespaces.Add(extension.ContainingType.ContainingNamespace.ToDisplayString());

        if (MemberReferences.IsImplicitInstanceMember(name, model, _source, out var member))
        {
            _needsInstance = true;
            NoteAccess(member);
            rewrites[name] = current => MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(_instanceField),
                    ((SimpleNameSyntax)current).WithoutTrivia())
                .WithTriviaFrom(current);
            return;
        }

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
            && MemberReferences.InheritsFrom(_source, symbol.ContainingType))
        {
            var owner = symbol.ContainingType;
            rewrites[name] = current => MemberAccessExpression(
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

    /// <summary>A private member of the old class the body uses becomes internal; a protected one cannot be reached.</summary>
    private void NoteAccess(ISymbol symbol)
    {
        if (SymbolEqualityComparer.Default.Equals(symbol, _method))
            return;
        if (symbol.DeclaredAccessibility is Accessibility.Protected or Accessibility.ProtectedAndInternal)
            throw new McpException($"Error: {_method.Name} uses the protected member {symbol.Name}, which {_className} cannot reach");
        if (symbol.DeclaredAccessibility == Accessibility.Private && symbol.Locations.Any(l => l.IsInSource))
            _raised.Add(symbol.OriginalDefinition);
    }

    /// <summary>
    /// A converted declaration becomes an assignment to the field for each
    /// local it initialises, and disappears when it initialises none.
    /// </summary>
    private static SyntaxNode ReplaceConvertedDeclarations(SyntaxNode root)
    {
        while (root.GetAnnotatedNodes(ConvertedDeclaration).OfType<LocalDeclarationStatementSyntax>().FirstOrDefault() is { } statement)
        {
            var assignments = statement.Declaration.Variables
                .Where(v => v.Initializer is not null)
                .Select(v => (StatementSyntax)ExpressionStatement(AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(FieldName(v.Identifier.ValueText)),
                    v.Initializer!.Value.WithoutTrivia())))
                .ToList();

            if (assignments.Count == 0)
            {
                var keep = statement.GetLeadingTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia));
                root = root.RemoveNode(statement, keep ? SyntaxRemoveOptions.KeepLeadingTrivia : SyntaxRemoveOptions.KeepNoTrivia)!;
                continue;
            }

            assignments[0] = assignments[0].WithLeadingTrivia(statement.GetLeadingTrivia());
            assignments[^1] = assignments[^1].WithTrailingTrivia(statement.GetTrailingTrivia());
            for (var i = 1; i < assignments.Count; i++)
                assignments[i] = assignments[i].WithAdditionalAnnotations(Formatter.Annotation);
            root = root.ReplaceNode(statement, assignments);
        }

        return root;
    }

    /// <summary>Names the body declares for itself must not hide the fields it now uses.</summary>
    private void EnsureNoNameConflicts(SyntaxNode body, SemanticModel model)
    {
        var fields = FieldNames().ToHashSet(StringComparer.Ordinal);
        var declared = body.DescendantNodes().Select(node => node switch
        {
            VariableDeclaratorSyntax variable when _locals.Contains(model.GetDeclaredSymbol(variable)!) => null,
            VariableDeclaratorSyntax variable => variable.Identifier.ValueText,
            ParameterSyntax parameter => parameter.Identifier.ValueText,
            SingleVariableDesignationSyntax designation => designation.Identifier.ValueText,
            ForEachStatementSyntax loop => loop.Identifier.ValueText,
            LocalFunctionStatementSyntax function => function.Identifier.ValueText,
            CatchDeclarationSyntax @catch => @catch.Identifier.ValueText,
            _ => null,
        });

        var clash = declared.FirstOrDefault(name => name is not null && fields.Contains(name));
        if (clash is not null)
            throw new McpException($"Error: {_method.Name} already declares '{clash}', which would hide a field of {_className}");

        if (_needsInstance && _method.Parameters.Any(p => p.Name == InstanceParameter))
            throw new McpException($"Error: {_method.Name} already has a parameter named {InstanceParameter}, which the constructor needs for the instance");
    }

    private IEnumerable<string> FieldNames()
    {
        if (_needsInstance)
            yield return _instanceField;
        foreach (var parameter in _method.Parameters)
            yield return FieldName(parameter.Name);
        foreach (var local in _locals)
            yield return FieldName(local.Name);
    }

    /// <summary>The old method's body becomes a call running a new method object.</summary>
    private MethodDeclarationSyntax CallMethodObject(MethodDeclarationSyntax method)
    {
        var arguments = new List<ArgumentSyntax>();
        if (_needsInstance)
            arguments.Add(Argument(ThisExpression()));
        arguments.AddRange(_method.Parameters.Select(p => Argument(IdentifierName(p.Name))));

        var run = InvocationExpression(MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            ObjectCreationExpression(IdentifierName(_className)).WithArgumentList(ArgumentList(SeparatedList(arguments))),
            IdentifierName(_computeName)))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var asyncKeyword = method.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.AsyncKeyword));
        if (!asyncKeyword.IsKind(SyntaxKind.None))
            method = method.WithModifiers(method.Modifiers.Remove(asyncKeyword));

        if (method.Body is { } block)
        {
            StatementSyntax statement = _method.ReturnsVoid ? ExpressionStatement(run) : ReturnStatement(run);
            return method.WithBody(block.WithStatements(SingletonList(statement.WithAdditionalAnnotations(Formatter.Annotation))));
        }

        return method.WithExpressionBody(method.ExpressionBody!.WithExpression(run.WithTriviaFrom(method.ExpressionBody.Expression)));
    }

    private ClassDeclarationSyntax BuildClass(MethodDeclarationSyntax declaration, SemanticModel model, SyntaxNode body)
    {
        var nullable = model.GetNullableContext(declaration.SpanStart).AnnotationsEnabled();
        var members = new List<MemberDeclarationSyntax>();
        if (_needsInstance)
            members.Add(Field(MovingSupport.QualifiedType(_source), _instanceField, readOnly: true));

        var assignedParameters = declaration.Body is null
            ? Array.Empty<ISymbol>()
            : model.AnalyzeDataFlow(declaration.Body)?.WrittenInside.ToArray() ?? Array.Empty<ISymbol>();
        foreach (var parameter in _method.Parameters)
        {
            var readOnly = !assignedParameters.Contains(parameter, SymbolEqualityComparer.Default);
            members.Add(Field(MovingSupport.QualifiedType(parameter.Type), FieldName(parameter.Name), readOnly));
        }

        foreach (var local in _locals.OrderBy(l => l.Locations[0].SourceSpan.Start))
        {
            // A reference type field is null until the body first assigns it.
            var type = nullable && local.Type.IsReferenceType ? local.Type.WithNullableAnnotation(NullableAnnotation.Annotated) : local.Type;
            members.Add(Field(MovingSupport.QualifiedType(type), FieldName(local.Name), readOnly: false));
        }

        var constructorParameters = new List<ParameterSyntax>();
        var assignments = new List<StatementSyntax>();
        if (_needsInstance)
        {
            constructorParameters.Add(Parameter(Identifier(InstanceParameter)).WithType(MovingSupport.QualifiedType(_source)));
            assignments.Add(Assign(_instanceField, InstanceParameter));
        }

        foreach (var parameter in _method.Parameters)
        {
            constructorParameters.Add(Parameter(Identifier(parameter.Name)).WithType(MovingSupport.QualifiedType(parameter.Type)));
            assignments.Add(Assign(FieldName(parameter.Name), parameter.Name));
        }

        var newLine = TypeDeclarations.NewLine(declaration.SyntaxTree.GetRoot());
        members.Add(ConstructorDeclaration(_className)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(ParameterList(SeparatedList(constructorParameters)))
            .WithBody(Block(assignments))
            .WithLeadingTrivia(newLine));

        var modifiers = TokenList(Token(SyntaxKind.PublicKeyword));
        foreach (var modifier in declaration.Modifiers.Where(m => m.Kind() is SyntaxKind.AsyncKeyword or SyntaxKind.UnsafeKeyword))
            modifiers = modifiers.Add(Token(modifier.Kind()));

        var compute = MethodDeclaration(MovingSupport.QualifiedType(_method.ReturnType), _computeName)
            .WithModifiers(modifiers)
            .WithLeadingTrivia(newLine);
        compute = body is BlockSyntax block
            ? compute.WithBody(block)
            : compute.WithExpressionBody(ArrowExpressionClause((ExpressionSyntax)body)).WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        members.Add(compute);

        var accessibility = IsPublic(_source) ? SyntaxKind.PublicKeyword : SyntaxKind.InternalKeyword;
        return ClassDeclaration(_className)
            .WithModifiers(TokenList(Token(accessibility)))
            .WithMembers(List(members))
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static bool IsPublic(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
                return false;
        }

        return true;
    }

    private static FieldDeclarationSyntax Field(TypeSyntax type, string name, bool readOnly)
    {
        var modifiers = TokenList(Token(SyntaxKind.PrivateKeyword));
        if (readOnly)
            modifiers = modifiers.Add(Token(SyntaxKind.ReadOnlyKeyword));
        return FieldDeclaration(VariableDeclaration(type, SingletonSeparatedList(VariableDeclarator(name))))
            .WithModifiers(modifiers);
    }

    private static StatementSyntax Assign(string field, string value) =>
        ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(field), IdentifierName(value)));

    /// <summary>A file for the class in the old class's namespace, written in the same namespace style.</summary>
    private static CompilationUnitSyntax CompilationUnitFor(MethodDeclarationSyntax declaration, ClassDeclarationSyntax type)
    {
        var ns = declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().LastOrDefault();
        MemberDeclarationSyntax member = ns switch
        {
            FileScopedNamespaceDeclarationSyntax fileScoped => FileScopedNamespaceDeclaration(fileScoped.Name.WithoutTrivia())
                .WithMembers(SingletonList<MemberDeclarationSyntax>(type)),
            NamespaceDeclarationSyntax block => NamespaceDeclaration(block.Name.WithoutTrivia())
                .WithMembers(SingletonList<MemberDeclarationSyntax>(type)),
            _ => type,
        };

        return CompilationUnit().WithMembers(SingletonList(member));
    }

    private async Task RaiseAsync(SyntaxEdits edits, ISymbol symbol, CancellationToken cancellationToken)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            var node = await reference.GetSyntaxAsync(cancellationToken);
            var declaration = node as MemberDeclarationSyntax ?? node.FirstAncestorOrSelf<MemberDeclarationSyntax>()!;
            edits.Replace(_solution, declaration, current =>
            {
                var member = (MemberDeclarationSyntax)current;
                return member.WithModifiers(MovingSupport.RaisePrivateToInternal(member.Modifiers));
            });
        }
    }
}
