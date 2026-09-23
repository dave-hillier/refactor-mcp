using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using ModelContextProtocol;
using RefactorMCP.ConsoleApp.Tools.Moving;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

/// <summary>The part of an interface member a generated body implements.</summary>
internal enum MemberPart
{
    Invoke,
    Get,
    Set,
    Add,
    Remove,
}

/// <summary>
/// Generates a class that implements an interface by wrapping one object in
/// a readonly field, as the decorator and adapter generators do, and writes
/// it to a new file. Each member's body comes from the caller; the class
/// shape, member order and layout are shared.
/// </summary>
internal static class InterfaceImplementation
{
    /// <summary>
    /// The instance methods, properties, indexers and events a class must
    /// implement: the interface's own, then those of each interface it
    /// inherits.
    /// </summary>
    public static IReadOnlyList<ISymbol> MembersToImplement(INamedTypeSymbol @interface) =>
        new[] { @interface }.Concat(@interface.AllInterfaces)
            .SelectMany(i => i.GetMembers())
            .Where(m => !m.IsStatic && m is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol or IEventSymbol)
            .ToList();

    /// <summary>
    /// <c>public class name : interface</c> holding the wrapped object in a
    /// readonly field set by its constructor, with a member per interface
    /// member. A member whose signature an earlier one already declares with
    /// the same type is implemented by that one; with another type, as it is
    /// when a derived interface hides it, it is implemented explicitly.
    /// </summary>
    public static ClassDeclarationSyntax WrapperClass(
        string name,
        INamedTypeSymbol @interface,
        ITypeSymbol wrappedType,
        string fieldName,
        string parameterName,
        SyntaxTrivia endOfLine,
        Func<ISymbol, bool, MemberPart, ExpressionSyntax> body)
    {
        var field = FieldDeclaration(VariableDeclaration(TypeFor(wrappedType)).AddVariables(VariableDeclarator(fieldName)))
            .AddModifiers(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ReadOnlyKeyword));
        var constructor = ConstructorDeclaration(name)
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(Parameter(Identifier(parameterName)).WithType(TypeFor(wrappedType)))
            .WithBody(Block(ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(fieldName),
                IdentifierName(parameterName)))));

        var members = new List<MemberDeclarationSyntax> { field, constructor };
        var implemented = new Dictionary<string, ISymbol>(StringComparer.Ordinal);
        foreach (var member in MembersToImplement(@interface))
        {
            var key = SignatureKey(member);
            var isExplicit = false;
            if (implemented.TryGetValue(key, out var earlier))
            {
                if (SymbolEqualityComparer.IncludeNullability.Equals(TypeOf(earlier), TypeOf(member)))
                    continue;
                isExplicit = true;
            }
            else
            {
                implemented[key] = member;
            }

            members.Add(Implement(member, isExplicit, endOfLine, part => body(member, isExplicit, part)));
        }

        var typeParameters = TypeParametersOf(@interface);
        var declaration = ClassDeclaration(name)
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddBaseListTypes(SimpleBaseType(TypeFor(@interface)))
            .WithMembers(List(members.Select((m, i) => (i == 0 ? m : m.WithLeadingTrivia(endOfLine)).WithTrailingTrivia(endOfLine))));
        if (typeParameters.Count > 0)
        {
            declaration = declaration
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParameters.Select(t => TypeParameter(t.Name)))))
                .WithConstraintClauses(List(typeParameters.Select(Constraints).OfType<TypeParameterConstraintClauseSyntax>()));
        }

        return declaration;
    }

    /// <summary>
    /// <c>receiver.name(arguments)</c>, <c>receiver.name</c>,
    /// <c>receiver[arguments] = value</c> and so on: the expression that
    /// forwards one part of <paramref name="member"/> to the member called
    /// <paramref name="name"/>.
    /// </summary>
    public static ExpressionSyntax Forward(ExpressionSyntax receiver, string name, ISymbol member, MemberPart part)
    {
        ExpressionSyntax Access() => member is IPropertySymbol { IsIndexer: true } indexer
            ? ElementAccessExpression(receiver, BracketedArgumentList(SeparatedList(indexer.Parameters.Select(ArgumentFor))))
            : MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, IdentifierName(name));

        ExpressionSyntax Value(SyntaxKind kind) => AssignmentExpression(kind, Access(), IdentifierName("value"));

        return part switch
        {
            MemberPart.Invoke => Invocation(receiver, name, (IMethodSymbol)member),
            MemberPart.Get => Access(),
            MemberPart.Set => Value(SyntaxKind.SimpleAssignmentExpression),
            MemberPart.Add => Value(SyntaxKind.AddAssignmentExpression),
            MemberPart.Remove => Value(SyntaxKind.SubtractAssignmentExpression),
            _ => throw new ArgumentOutOfRangeException(nameof(part)),
        };
    }

    /// <summary><c>throw new NotImplementedException()</c>, for a member with nothing to forward to.</summary>
    public static ExpressionSyntax NotImplemented() =>
        ThrowExpression(ObjectCreationExpression(TypeFor("System.NotImplementedException")).WithArgumentList(ArgumentList()));

    /// <summary>A type name the simplifier shortens and imports.</summary>
    public static TypeSyntax TypeFor(ITypeSymbol type) => MovingSupport.QualifiedType(type);

    private static TypeSyntax TypeFor(string metadataName) =>
        ParseTypeName("global::" + metadataName)
            .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation);

    /// <summary>
    /// Refuses a type name that is not an identifier, or that the namespace
    /// or the folder already has.
    /// </summary>
    public static void EnsureNameIsFree(INamespaceSymbol ns, string name, string filePath)
    {
        if (!SyntaxFacts.IsValidIdentifier(name))
            throw new McpException($"Error: '{name}' is not a valid type name");

        var qualified = ns.IsGlobalNamespace ? name : $"{ns.ToDisplayString()}.{name}";
        if (ns.GetTypeMembers(name).Any())
            throw new McpException($"Error: A type named {qualified} already exists");
        if (File.Exists(filePath))
            throw new McpException($"Error: The file {filePath} for {qualified} already exists");
    }

    /// <summary>
    /// The solution with <paramref name="type"/> declared in a new file named
    /// after it beside <paramref name="beside"/>, in <paramref name="ns"/>
    /// written in the namespace style <paramref name="beside"/> uses, with the
    /// usings it needs.
    /// </summary>
    public static async Task<Solution> AddTypeFileAsync(
        Document beside,
        INamespaceSymbol ns,
        BaseTypeDeclarationSyntax type,
        CancellationToken cancellationToken)
    {
        var root = (CompilationUnitSyntax)(await beside.GetSyntaxRootAsync(cancellationToken))!;
        var fileScoped = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().Any();

        MemberDeclarationSyntax member = type;
        if (!ns.IsGlobalNamespace)
        {
            var name = ParseName(ns.ToDisplayString());
            member = fileScoped
                ? FileScopedNamespaceDeclaration(name).AddMembers(type)
                : NamespaceDeclaration(name).AddMembers(type);
        }

        var unit = CompilationUnit().AddMembers(member).WithAdditionalAnnotations(Formatter.Annotation);
        var path = PathFor(beside, type.Identifier.ValueText);
        var document = MovingSupport.AddDocument(beside.Project, path, unit);
        document = await MovingSupport.TidyAsync(document, cancellationToken);

        // The formatter leaves generated event accessors unindented, so the
        // tidied text is parsed again and formatted as written code.
        document = document.WithText(TypeRefactoringHelpers.Text((await document.GetTextAsync(cancellationToken)).ToString()));
        document = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);

        // The file ends with a line break, as the files beside it do.
        var formatted = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
        var last = formatted.GetLastToken();
        if (!last.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia))
            document = document.WithSyntaxRoot(formatted.ReplaceToken(last, last.WithTrailingTrivia(TypeRefactoringHelpers.EndOfLine(root))));
        return document.Project.Solution;
    }

    /// <summary>The path of a new file for a type named <paramref name="typeName"/> beside <paramref name="document"/>.</summary>
    public static string PathFor(Document document, string typeName) =>
        Path.Combine(Path.GetDirectoryName(document.FilePath!)!, typeName + ".cs");

    private static MemberDeclarationSyntax Implement(ISymbol member, bool isExplicit, SyntaxTrivia endOfLine, Func<MemberPart, ExpressionSyntax> body)
    {
        var owner = member.ContainingType;
        var modifiers = isExplicit ? TokenList() : TokenList(Token(SyntaxKind.PublicKeyword));
        var specifier = isExplicit ? ExplicitInterfaceSpecifier((NameSyntax)TypeFor(owner)) : null;

        switch (member)
        {
            case IMethodSymbol method:
                var returnType = method.ReturnsVoid ? PredefinedType(Token(SyntaxKind.VoidKeyword)) : TypeFor(method.ReturnType);
                var declaration = MethodDeclaration(returnType, method.Name)
                    .WithModifiers(modifiers)
                    .WithExplicitInterfaceSpecifier(specifier)
                    .WithParameterList(ParameterList(SeparatedList(method.Parameters.Select(ParameterFor))))
                    .WithExpressionBody(ArrowExpressionClause(body(MemberPart.Invoke)))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
                if (method.TypeParameters.Length > 0)
                {
                    declaration = declaration.WithTypeParameterList(
                        TypeParameterList(SeparatedList(method.TypeParameters.Select(t => TypeParameter(t.Name)))));
                    if (!isExplicit)
                        declaration = declaration.WithConstraintClauses(List(method.TypeParameters.Select(Constraints).OfType<TypeParameterConstraintClauseSyntax>()));
                }

                return declaration;

            case IPropertySymbol property:
                var accessors = new List<AccessorDeclarationSyntax>();
                if (property.GetMethod is not null)
                    accessors.Add(Accessor(SyntaxKind.GetAccessorDeclaration, body(MemberPart.Get)));
                if (property.SetMethod is not null)
                    accessors.Add(Accessor(SyntaxKind.SetAccessorDeclaration, body(MemberPart.Set)));

                if (property.IsIndexer)
                {
                    var indexer = IndexerDeclaration(TypeFor(property.Type))
                        .WithModifiers(modifiers)
                        .WithExplicitInterfaceSpecifier(specifier)
                        .WithParameterList(BracketedParameterList(SeparatedList(property.Parameters.Select(ParameterFor))));
                    return accessors.Count == 1 && property.SetMethod is null
                        ? indexer.WithExpressionBody(ArrowExpressionClause(body(MemberPart.Get))).WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                        : indexer.WithAccessorList(Accessors(accessors, endOfLine));
                }

                var declared = PropertyDeclaration(TypeFor(property.Type), property.Name)
                    .WithModifiers(modifiers)
                    .WithExplicitInterfaceSpecifier(specifier);
                return property.SetMethod is null
                    ? declared.WithExpressionBody(ArrowExpressionClause(body(MemberPart.Get))).WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                    : declared.WithAccessorList(Accessors(accessors, endOfLine));

            case IEventSymbol @event:
                return EventDeclaration(
                    default,
                    modifiers,
                    Token(SyntaxKind.EventKeyword),
                    TypeFor(@event.Type),
                    specifier,
                    Identifier(@event.Name),
                    Accessors(
                        new[]
                        {
                            Accessor(SyntaxKind.AddAccessorDeclaration, body(MemberPart.Add)),
                            Accessor(SyntaxKind.RemoveAccessorDeclaration, body(MemberPart.Remove)),
                        },
                        endOfLine),
                    default);

            default:
                throw new McpException($"Error: {member.Name} is not a member that can be implemented");
        }
    }

    /// <summary>
    /// An accessor list laid out over several lines, one accessor to a line.
    /// The line breaks carry no elastic trivia, which the formatter would be
    /// free to fold onto one line; it only indents them.
    /// </summary>
    private static AccessorListSyntax Accessors(IEnumerable<AccessorDeclarationSyntax> accessors, SyntaxTrivia endOfLine) =>
        AccessorList(
            Token(SyntaxKind.OpenBraceToken).WithLeadingTrivia(ElasticMarker).WithTrailingTrivia(endOfLine),
            List(accessors.Select(a => a.WithLeadingTrivia().WithTrailingTrivia(endOfLine))),
            Token(SyntaxKind.CloseBraceToken).WithLeadingTrivia().WithTrailingTrivia(ElasticMarker));

    private static AccessorDeclarationSyntax Accessor(SyntaxKind kind, ExpressionSyntax body) =>
        AccessorDeclaration(kind)
            .WithExpressionBody(ArrowExpressionClause(body))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

    private static ExpressionSyntax Invocation(ExpressionSyntax receiver, string name, IMethodSymbol method)
    {
        SimpleNameSyntax target = method.TypeParameters.Length > 0
            ? GenericName(Identifier(name), TypeArgumentList(SeparatedList<TypeSyntax>(method.TypeParameters.Select(t => IdentifierName(t.Name)))))
            : IdentifierName(name);
        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, target),
            ArgumentList(SeparatedList(method.Parameters.Select(ArgumentFor))));
    }

    private static ArgumentSyntax ArgumentFor(IParameterSymbol parameter)
    {
        var argument = SyntaxFactory.Argument(IdentifierName(Escape(parameter.Name)));
        return parameter.RefKind switch
        {
            RefKind.Ref => argument.WithRefKindKeyword(Token(SyntaxKind.RefKeyword)),
            RefKind.Out => argument.WithRefKindKeyword(Token(SyntaxKind.OutKeyword)),
            RefKind.In => argument.WithRefKindKeyword(Token(SyntaxKind.InKeyword)),
            _ => argument,
        };
    }

    private static ParameterSyntax ParameterFor(IParameterSymbol parameter)
    {
        var modifiers = new List<SyntaxToken>();
        if (parameter.IsParams)
            modifiers.Add(Token(SyntaxKind.ParamsKeyword));
        modifiers.AddRange(parameter.RefKind switch
        {
            RefKind.Ref => new[] { Token(SyntaxKind.RefKeyword) },
            RefKind.Out => new[] { Token(SyntaxKind.OutKeyword) },
            RefKind.In => new[] { Token(SyntaxKind.InKeyword) },
            _ => Array.Empty<SyntaxToken>(),
        });

        var declared = Parameter(Identifier(Escape(parameter.Name)))
            .WithModifiers(TokenList(modifiers))
            .WithType(TypeFor(parameter.Type));
        return parameter.HasExplicitDefaultValue
            ? declared.WithDefault(EqualsValueClause(DefaultValue(parameter)))
            : declared;
    }

    private static ExpressionSyntax DefaultValue(IParameterSymbol parameter)
    {
        var value = parameter.ExplicitDefaultValue;
        if (value is null)
            return parameter.Type.IsValueType && parameter.Type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T
                ? LiteralExpression(SyntaxKind.DefaultLiteralExpression, Token(SyntaxKind.DefaultKeyword))
                : LiteralExpression(SyntaxKind.NullLiteralExpression);

        var literal = ParseExpression(SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false));
        return parameter.Type.TypeKind == TypeKind.Enum
            ? CastExpression(TypeFor(parameter.Type), ParenthesizedExpression(literal))
            : literal;
    }

    private static string Escape(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    /// <summary>
    /// The type parameters a wrapper of <paramref name="interface"/> needs:
    /// the interface's own when it is not constructed, otherwise those its
    /// type arguments mention, such as a generic class's.
    /// </summary>
    private static IReadOnlyList<ITypeParameterSymbol> TypeParametersOf(INamedTypeSymbol @interface)
    {
        if (SymbolEqualityComparer.Default.Equals(@interface, @interface.OriginalDefinition))
            return @interface.TypeParameters;

        var found = new List<ITypeParameterSymbol>();
        void Visit(ITypeSymbol type)
        {
            switch (type)
            {
                case ITypeParameterSymbol parameter when !found.Contains(parameter, SymbolEqualityComparer.Default):
                    found.Add(parameter);
                    break;
                case INamedTypeSymbol named:
                    foreach (var argument in named.TypeArguments)
                        Visit(argument);
                    break;
                case IArrayTypeSymbol array:
                    Visit(array.ElementType);
                    break;
            }
        }

        Visit(@interface);
        return found;
    }

    private static TypeParameterConstraintClauseSyntax? Constraints(ITypeParameterSymbol parameter)
    {
        var constraints = new List<TypeParameterConstraintSyntax>();
        if (parameter.HasReferenceTypeConstraint)
            constraints.Add(ClassOrStructConstraint(SyntaxKind.ClassConstraint));
        if (parameter.HasUnmanagedTypeConstraint)
            constraints.Add(TypeConstraint(IdentifierName("unmanaged")));
        else if (parameter.HasValueTypeConstraint)
            constraints.Add(ClassOrStructConstraint(SyntaxKind.StructConstraint));
        if (parameter.HasNotNullConstraint)
            constraints.Add(TypeConstraint(IdentifierName("notnull")));
        constraints.AddRange(parameter.ConstraintTypes.Select(t => TypeConstraint(TypeFor(t))));
        if (parameter.HasConstructorConstraint)
            constraints.Add(ConstructorConstraint());

        return constraints.Count == 0
            ? null
            : TypeParameterConstraintClause(IdentifierName(parameter.Name), SeparatedList(constraints));
    }

    /// <summary>What two members must share for one declaration to implement both.</summary>
    private static string SignatureKey(ISymbol member) => member switch
    {
        IMethodSymbol method => $"M:{method.Name}`{method.Arity}({string.Join(",", method.Parameters.Select(p => $"{p.RefKind} {p.Type.ToDisplayString()}"))})",
        IPropertySymbol { IsIndexer: true } indexer => $"I:({string.Join(",", indexer.Parameters.Select(p => p.Type.ToDisplayString()))})",
        _ => $"P:{member.Name}",
    };

    private static ITypeSymbol? TypeOf(ISymbol member) => member switch
    {
        IMethodSymbol method => method.ReturnType,
        IPropertySymbol property => property.Type,
        IEventSymbol @event => @event.Type,
        _ => null,
    };
}
