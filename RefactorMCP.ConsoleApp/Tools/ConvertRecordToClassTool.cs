using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

[McpServerToolType]
public static class ConvertRecordToClassTool
{
    [McpServerTool, Description("Convert a record to a class, generating the constructor, properties, Deconstruct, equality, ToString and operators the record provided")]
    public static async Task<string> ConvertRecordToClass(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the record")] string filePath,
        [Description("Name of the record")] string typeName,
        [Description("A line of the declaration (1-based), to choose between types of the same name")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var type = await TypeDeclarations.FindTypeAsync(solution, filePath, typeName, line, cancellationToken);
            var record = await EnsureConvertibleAsync(solution, type, cancellationToken);

            var document = solution.GetDocument(record.SyntaxTree)!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var shape = Shape.Of(record, type, model);

            // The primary constructor's parameters become camel case, so named arguments follow.
            var primary = type.InstanceConstructors.FirstOrDefault(c =>
                c.DeclaringSyntaxReferences.Any(r => r.GetSyntax(cancellationToken) is RecordDeclarationSyntax));
            var renames = await TypeDeclarations.NamedArgumentRenamesAsync(
                solution,
                primary,
                shape.Positional.ToDictionary(p => p.Name, p => GeneratedMembers.ParameterName(p.Name)),
                cancellationToken);

            var changed = solution;
            foreach (var group in renames.Where(g => g.Key != document.Id))
            {
                var root = await solution.GetDocument(group.Key)!.GetSyntaxRootAsync(cancellationToken);
                changed = changed.WithDocumentSyntaxRoot(group.Key, Rename(root!, group));
            }

            var recordRoot = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
            var ownRenames = renames[document.Id].ToDictionary(r => (SyntaxNode)r.Name, r => r.NewName);
            recordRoot = recordRoot.ReplaceNodes(
                ownRenames.Keys.Append(record),
                (original, rewritten) => original == record
                    ? ToClass((RecordDeclarationSyntax)rewritten, shape)
                    : Renamed((IdentifierNameSyntax)rewritten, ownRenames[original]));
            recordRoot = TypeDeclarations.WithUsings(
                recordRoot,
                TypeDeclarations.MissingImports(model, record.SpanStart, shape.Imports()));
            changed = changed.WithDocumentSyntaxRoot(document.Id, recordRoot);

            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully converted record '{typeName}' to a class";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting record to class: {ex.Message}", ex);
        }
    }

    private static async Task<RecordDeclarationSyntax> EnsureConvertibleAsync(
        Solution solution,
        INamedTypeSymbol type,
        CancellationToken cancellationToken)
    {
        if (!type.IsRecord)
            throw new McpException($"Error: '{type.Name}' is not a record");

        if (type.TypeKind == TypeKind.Struct)
            throw new McpException(
                $"Error: '{type.Name}' is a record struct; converting it to a class would change how it is copied");

        var declarations = await TypeDeclarations.DeclarationsAsync(type, cancellationToken);
        if (declarations.Count > 1)
            throw new McpException($"Error: '{type.Name}' has {declarations.Count} declarations; merge them first");

        if (type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            throw new McpException(
                $"Error: '{type.Name}' is part of a record hierarchy: it derives from '{baseType.Name}'");

        var derived = await SymbolFinder.FindDerivedClassesAsync(type, solution, transitive: false, cancellationToken: cancellationToken);
        if (derived.FirstOrDefault() is { } first)
            throw new McpException(
                $"Error: '{type.Name}' is part of a record hierarchy: '{first.Name}' derives from it");

        foreach (var document in solution.Projects.SelectMany(p => p.Documents))
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var copies = root!.DescendantNodes().OfType<WithExpressionSyntax>().ToList();
            if (copies.Count == 0)
                continue;

            var model = await document.GetSemanticModelAsync(cancellationToken);
            var copy = copies.FirstOrDefault(w =>
                SymbolEqualityComparer.Default.Equals(model!.GetTypeInfo(w.Expression, cancellationToken).Type?.OriginalDefinition, type));
            if (copy is not null)
                throw new McpException(
                    $"Error: '{type.Name}' is copied with a with expression at {SolutionEdits.Describe(copy.GetLocation())}, which a class does not support");
        }

        return (RecordDeclarationSyntax)declarations[0];
    }

    /// <summary>What the record declares and what the compiler would have generated for it.</summary>
    private sealed record Shape(
        IReadOnlyList<GeneratedValue> Positional,
        IReadOnlyList<GeneratedValue> Fields,
        IReadOnlyList<GeneratedValue> Printed,
        bool IsSealed,
        bool Annotate,
        bool DeclaresTypedEquals,
        bool DeclaresGetHashCode,
        bool DeclaresToString,
        bool DeclaresDeconstruct)
    {
        public static Shape Of(RecordDeclarationSyntax record, INamedTypeSymbol type, SemanticModel model)
        {
            var positional = (record.ParameterList?.Parameters ?? default)
                .Select(p => new GeneratedValue(p.Identifier.ValueText, p.Type!.ToString()))
                .ToList();
            var fields = new List<GeneratedValue>(positional);
            var printed = new List<GeneratedValue>(positional);

            foreach (var member in record.Members)
            {
                switch (member)
                {
                    case FieldDeclarationSyntax field when !field.Modifiers.Any(SyntaxKind.StaticKeyword) && !field.Modifiers.Any(SyntaxKind.ConstKeyword):
                        foreach (var variable in field.Declaration.Variables)
                        {
                            var value = new GeneratedValue(variable.Identifier.ValueText, field.Declaration.Type.ToString());
                            fields.Add(value);
                            if (model.GetDeclaredSymbol(variable)?.DeclaredAccessibility == Accessibility.Public)
                                printed.Add(value);
                        }
                        break;
                    case PropertyDeclarationSyntax property when !property.Modifiers.Any(SyntaxKind.StaticKeyword):
                        var symbol = model.GetDeclaredSymbol(property)!;
                        var propertyValue = new GeneratedValue(property.Identifier.ValueText, property.Type.ToString());
                        if (IsAutoProperty(property))
                            fields.Add(propertyValue);
                        if (symbol.DeclaredAccessibility == Accessibility.Public && symbol.GetMethod is not null && !symbol.IsOverride)
                            printed.Add(propertyValue);
                        break;
                }
            }

            var methods = type.GetMembers().OfType<IMethodSymbol>().Where(m => !m.IsImplicitlyDeclared).ToList();
            return new Shape(
                positional,
                fields,
                printed,
                type.IsSealed,
                TypeDeclarations.AnnotationsEnabled(model, record.SpanStart),
                methods.Any(m => m.Name == "Equals" && m.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type.OriginalDefinition, type)),
                methods.Any(m => m.Name == "GetHashCode" && m.Parameters.Length == 0),
                methods.Any(m => m.Name == "ToString" && m.Parameters.Length == 0),
                methods.Any(m => m.Name == "Deconstruct" && m.Parameters.Length == positional.Count));
        }

        /// <summary>The types the generated members name, for the imports the file needs.</summary>
        public (string Namespace, string Type)[] Imports()
        {
            var imports = new List<(string, string)> { ("System", "IEquatable") };
            if (!DeclaresTypedEquals && Fields.Count > 0)
                imports.Add(("System.Collections.Generic", "EqualityComparer"));
            if (!DeclaresGetHashCode && Fields.Count > 0)
                imports.Add(("System", "HashCode"));
            return imports.ToArray();
        }

        private static bool IsAutoProperty(PropertyDeclarationSyntax property) =>
            property.ExpressionBody is null
            && property.AccessorList is { } accessors
            && accessors.Accessors.All(a => a.Body is null && a.ExpressionBody is null);
    }

    /// <summary>
    /// The class: the record's header with <c>class</c> for <c>record</c> and
    /// <c>IEquatable&lt;T&gt;</c> added, its positional parameters written
    /// out, its members, then the members the compiler generated.
    /// </summary>
    private static ClassDeclarationSyntax ToClass(RecordDeclarationSyntax record, Shape shape)
    {
        var newLine = TypeDeclarations.NewLine(record);
        var typeIndentation = GeneratedMembers.Indentation(record.GetFirstToken());
        var indentation = record.Members.Count > 0
            ? GeneratedMembers.Indentation(record.Members[0].GetFirstToken())
            : typeIndentation + "    ";
        var typeName = record.Identifier.ValueText + record.TypeParameterList;
        var members = new List<MemberDeclarationSyntax>();

        void Add(string text) => members.Add(GeneratedMembers.Parse(text, indentation, newLine, blankLineBefore: members.Count > 0));

        if (shape.Positional.Count > 0)
        {
            Add(GeneratedMembers.Constructor(record.Identifier.ValueText, shape.Positional));
            foreach (var property in shape.Positional)
                Add(GeneratedMembers.Property(property, "get; init;"));
        }

        foreach (var member in record.Members)
        {
            members.Add(members.Count > 0 && member == record.Members[0]
                ? member.WithLeadingTrivia(member.GetLeadingTrivia().Insert(0, newLine))
                : member);
        }

        if (shape.Positional.Count > 0 && !shape.DeclaresDeconstruct)
            Add(GeneratedMembers.Deconstruct(shape.Positional));
        if (!shape.DeclaresTypedEquals)
            Add(GeneratedMembers.TypedEquals(typeName, shape.Fields, shape.IsSealed, shape.Annotate));
        Add(GeneratedMembers.ObjectEqualsCallingTyped(typeName, shape.Annotate));
        if (!shape.DeclaresGetHashCode)
            Add(GeneratedMembers.GetHashCodeMethod(shape.Fields));
        if (!shape.DeclaresToString)
            Add(GeneratedMembers.ToStringMethod(record.Identifier.ValueText, shape.Printed));
        foreach (var @operator in GeneratedMembers.EqualityOperators(typeName, shape.Annotate))
            Add(@operator);

        var keyword = SyntaxFactory.Token(
            record.Keyword.LeadingTrivia,
            SyntaxKind.ClassKeyword,
            record.ClassOrStructKeyword.IsKind(SyntaxKind.None) ? record.Keyword.TrailingTrivia : record.ClassOrStructKeyword.TrailingTrivia);

        // A record declared with a semicolon gets braces on their own lines.
        var hasBraces = !record.OpenBraceToken.IsKind(SyntaxKind.None);
        var openBrace = hasBraces
            ? record.OpenBraceToken
            : SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                .WithLeadingTrivia(SyntaxFactory.Whitespace(typeIndentation))
                .WithTrailingTrivia(newLine);
        var closeBrace = hasBraces
            ? record.CloseBraceToken
            : SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                .WithLeadingTrivia(SyntaxFactory.Whitespace(typeIndentation))
                .WithTrailingTrivia(record.SemicolonToken.TrailingTrivia);

        var result = SyntaxFactory.ClassDeclaration(
            record.AttributeLists,
            record.Modifiers,
            keyword,
            record.Identifier,
            record.TypeParameterList,
            record.BaseList,
            record.ConstraintClauses,
            openBrace,
            SyntaxFactory.List(members),
            closeBrace,
            default);

        // The parameter list's trivia, often the line break before the brace, stays in the header.
        if (record.ParameterList is { } parameters)
        {
            var beforeParameters = NameEnd(result);
            result = result.ReplaceToken(beforeParameters, beforeParameters.WithTrailingTrivia(parameters.CloseParenToken.TrailingTrivia));
        }

        result = WithEquatable(result, typeName);

        if (!hasBraces)
        {
            var headerEnd = result.OpenBraceToken.GetPreviousToken();
            result = result.ReplaceToken(headerEnd, headerEnd.WithTrailingTrivia(newLine));
        }

        return result;
    }

    /// <summary>The last token of a type's name: its identifier or the end of its type parameters.</summary>
    private static SyntaxToken NameEnd(TypeDeclarationSyntax type) =>
        type.TypeParameterList is { } typeParameters ? typeParameters.GreaterThanToken : type.Identifier;

    /// <summary>Adds <c>IEquatable&lt;T&gt;</c> as the last base type, before any constraint.</summary>
    private static ClassDeclarationSyntax WithEquatable(ClassDeclarationSyntax type, string typeName)
    {
        var equatable = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName($"IEquatable<{typeName}>"));
        if (type.BaseList is { } baseList)
        {
            var last = baseList.Types.Last();
            var types = baseList.Types
                .Replace(last, last.WithTrailingTrivia())
                .Add(equatable.WithTrailingTrivia(last.GetTrailingTrivia()));
            return type.WithBaseList(baseList.WithTypes(types));
        }

        var before = NameEnd(type);
        var ending = before.TrailingTrivia;
        type = type.ReplaceToken(before, before.WithTrailingTrivia(SyntaxFactory.Space));
        return type.WithBaseList(SyntaxFactory.BaseList(
            SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(equatable.WithTrailingTrivia(ending))));
    }

    private static SyntaxNode Rename(SyntaxNode root, IEnumerable<(IdentifierNameSyntax Name, string NewName)> renames)
    {
        var map = renames.ToDictionary(r => r.Name, r => r.NewName);
        return root.ReplaceNodes(map.Keys, (original, rewritten) => Renamed(rewritten, map[original]));
    }

    private static IdentifierNameSyntax Renamed(IdentifierNameSyntax name, string newName) =>
        name.WithIdentifier(SyntaxFactory.Identifier(name.Identifier.LeadingTrivia, newName, name.Identifier.TrailingTrivia));
}
