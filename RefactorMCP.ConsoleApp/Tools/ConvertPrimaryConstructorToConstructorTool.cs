using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[McpServerToolType]
public static class ConvertPrimaryConstructorToConstructorTool
{
    [McpServerTool, Description("Replace a class or struct's primary constructor with an explicit constructor, storing captured parameters in private fields")]
    public static async Task<string> ConvertPrimaryConstructorToConstructor(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the class or struct")] string filePath,
        [Description("Name of the class or struct")] string typeName,
        [Description("A line of the declaration (1-based), to choose between types of the same name")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var type = await TypeDeclarations.FindTypeAsync(solution, filePath, typeName, line, cancellationToken);
            var declaration = await TypeDeclarations.SingleDeclarationAsync(type, cancellationToken) as TypeDeclarationSyntax;

            if (type.IsRecord || declaration is not (ClassDeclarationSyntax or StructDeclarationSyntax) || type.DeclaringSyntaxReferences.Length > 1)
                throw new McpException($"Error: '{typeName}' is not a class or struct with a single declaration, so its primary constructor cannot be written out");

            if (declaration.ParameterList is null)
                throw new McpException($"Error: '{typeName}' has no primary constructor");

            var document = solution.GetDocument(declaration.SyntaxTree)!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var plan = Plan.Of(declaration, type, model, cancellationToken);

            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            root = root.ReplaceNodes(
                plan.CapturedUses.Keys.Concat(plan.MovedInitializers.Select(m => m.Member)).Append(declaration),
                (original, rewritten) => original switch
                {
                    TypeDeclarationSyntax when original == declaration => WithConstructor((TypeDeclarationSyntax)rewritten, declaration, plan),
                    IdentifierNameSyntax => SyntaxFactory.IdentifierName(plan.CapturedUses[original]).WithTriviaFrom(rewritten),
                    _ => WithoutInitializer(rewritten),
                });

            var changed = solution.WithDocumentSyntaxRoot(document.Id, root);
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully replaced the primary constructor of '{typeName}' with a constructor";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting the primary constructor: {ex.Message}", ex);
        }
    }

    /// <summary>A member whose initializer reads a parameter, and so moves into the constructor.</summary>
    private sealed record MovedInitializer(SyntaxNode Member, string Name, ExpressionSyntax Value);

    /// <summary>A captured parameter and the field that will hold it.</summary>
    private sealed record CapturedParameter(ParameterSyntax Syntax, string Field, bool IsWritten);

    private sealed record Plan(
        IReadOnlyList<CapturedParameter> Captured,
        IReadOnlyDictionary<SyntaxNode, string> CapturedUses,
        IReadOnlyList<MovedInitializer> MovedInitializers)
    {
        /// <summary>
        /// Sorts each parameter use into a base argument, an initializer that
        /// moves into the constructor, or a capture that needs a field.
        /// </summary>
        public static Plan Of(TypeDeclarationSyntax declaration, INamedTypeSymbol type, SemanticModel model, CancellationToken cancellationToken)
        {
            var parameters = declaration.ParameterList!.Parameters
                .Select(p => (Syntax: p, Symbol: model.GetDeclaredSymbol(p, cancellationToken)!))
                .ToList();
            var captured = new List<CapturedParameter>();
            var uses = new Dictionary<SyntaxNode, string>();
            var moved = new List<MovedInitializer>();

            foreach (var member in declaration.Members)
            {
                foreach (var (node, name, value) in Initializers(member))
                {
                    var readsParameter = value.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                        .Any(n => model.GetSymbolInfo(n, cancellationToken).Symbol is IParameterSymbol p
                            && parameters.Any(q => SymbolEqualityComparer.Default.Equals(q.Symbol, p)));
                    if (readsParameter)
                        moved.Add(new MovedInitializer(node, name, value));
                }
            }

            foreach (var (syntax, symbol) in parameters)
            {
                var memberUses = declaration.Members
                    .SelectMany(m => m.DescendantNodes())
                    .OfType<IdentifierNameSyntax>()
                    .Where(n => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(n, cancellationToken).Symbol, symbol))
                    .Where(n => !moved.Any(m => m.Value.Span.Contains(n.Span)))
                    .ToList();
                if (memberUses.Count == 0)
                    continue;

                var field = "_" + symbol.Name;
                if (type.GetMembers(field).Any())
                    throw new McpException(
                        $"Error: '{symbol.Name}' needs a field named '{field}', but '{type.Name}' already has a member of that name");

                captured.Add(new CapturedParameter(syntax, field, memberUses.Any(IsWritten)));
                foreach (var use in memberUses)
                    uses[use] = field;
            }

            return new Plan(captured, uses, moved);
        }

        private static IEnumerable<(SyntaxNode Node, string Name, ExpressionSyntax Value)> Initializers(MemberDeclarationSyntax member)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field when !field.Modifiers.Any(SyntaxKind.StaticKeyword) && !field.Modifiers.Any(SyntaxKind.ConstKeyword):
                    foreach (var variable in field.Declaration.Variables)
                    {
                        if (variable.Initializer is { } initializer)
                            yield return (variable, variable.Identifier.ValueText, initializer.Value);
                    }
                    break;
                case PropertyDeclarationSyntax { Initializer: { } initializer } property when !property.Modifiers.Any(SyntaxKind.StaticKeyword):
                    yield return (property, property.Identifier.ValueText, initializer.Value);
                    break;
            }
        }

        private static bool IsWritten(IdentifierNameSyntax use) => use.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == use,
            PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => true,
            ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None),
            _ => false,
        };
    }

    /// <summary>A field declarator or property without its initializer.</summary>
    private static SyntaxNode WithoutInitializer(SyntaxNode member) => member switch
    {
        VariableDeclaratorSyntax variable => variable
            .WithIdentifier(variable.Identifier.WithTrailingTrivia())
            .WithInitializer(null),
        PropertyDeclarationSyntax property => property
            .WithAccessorList(property.AccessorList!.WithTrailingTrivia(property.SemicolonToken.TrailingTrivia))
            .WithInitializer(null)
            .WithSemicolonToken(default),
        _ => member,
    };

    /// <summary>
    /// The type without its parameter list, with the fields for captured
    /// parameters first and the constructor after the last field.
    /// </summary>
    private static TypeDeclarationSyntax WithConstructor(TypeDeclarationSyntax type, TypeDeclarationSyntax original, Plan plan)
    {
        var newLine = TypeDeclarations.NewLine(original);
        var typeIndentation = GeneratedMembers.Indentation(original.GetFirstToken());
        var indentation = original.Members.Count > 0
            ? GeneratedMembers.Indentation(original.Members[0].GetFirstToken())
            : typeIndentation + "    ";

        var members = new List<MemberDeclarationSyntax>();
        foreach (var captured in plan.Captured)
        {
            var modifiers = captured.IsWritten ? "private" : "private readonly";
            members.Add(GeneratedMembers.Parse($"{modifiers} {captured.Syntax.Type} {captured.Field};", indentation, newLine, blankLineBefore: false));
        }

        foreach (var member in type.Members)
        {
            members.Add(members.Count > 0 && member == type.Members[0] && member is not FieldDeclarationSyntax
                ? WithBlankLine(member, newLine)
                : member);
        }

        var position = members.FindLastIndex(m => m is FieldDeclarationSyntax) + 1;
        members.Insert(position, GeneratedMembers.Parse(ConstructorText(original, plan), indentation, newLine, blankLineBefore: position > 0));
        if (position + 1 < members.Count)
            members[position + 1] = WithBlankLine(members[position + 1], newLine);

        var parameters = type.ParameterList!;
        var nameEnd = type.TypeParameterList?.GreaterThanToken ?? type.Identifier;
        var result = type
            .ReplaceToken(nameEnd, nameEnd.WithTrailingTrivia(parameters.GetTrailingTrivia()))
            .WithParameterList(null)
            .WithMembers(SyntaxFactory.List(members));

        if (result.BaseList is { } baseList && baseList.Types[0] is PrimaryConstructorBaseTypeSyntax primary)
        {
            var simple = SyntaxFactory.SimpleBaseType(primary.Type).WithTrailingTrivia(primary.GetTrailingTrivia());
            result = result.WithBaseList(baseList.WithTypes(baseList.Types.Replace(primary, simple)));
        }

        if (result.OpenBraceToken.IsKind(SyntaxKind.None))
        {
            // A type declared with a semicolon gets braces on their own lines.
            var headerEnd = result.SemicolonToken.GetPreviousToken();
            result = result
                .ReplaceToken(headerEnd, headerEnd.WithTrailingTrivia(newLine))
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithLeadingTrivia(SyntaxFactory.Whitespace(typeIndentation)).WithTrailingTrivia(newLine))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithLeadingTrivia(SyntaxFactory.Whitespace(typeIndentation)).WithTrailingTrivia(result.SemicolonToken.TrailingTrivia))
                .WithSemicolonToken(default);
        }

        return result;
    }

    private static string ConstructorText(TypeDeclarationSyntax type, Plan plan)
    {
        var baseCall = type.BaseList?.Types[0] is PrimaryConstructorBaseTypeSyntax primary
            ? $" : base{primary.ArgumentList}"
            : "";
        var text = new StringBuilder();
        text.AppendLine($"public {type.Identifier.ValueText}{type.ParameterList!.WithoutTrivia()}{baseCall}");
        text.AppendLine("{");
        foreach (var captured in plan.Captured)
            text.AppendLine($"    {captured.Field} = {captured.Syntax.Identifier.Text};");
        foreach (var moved in plan.MovedInitializers)
            text.AppendLine($"    {moved.Name} = {moved.Value};");
        text.Append('}');
        return text.ToString();
    }

    private static MemberDeclarationSyntax WithBlankLine(MemberDeclarationSyntax member, SyntaxTrivia newLine)
    {
        var leading = member.GetLeadingTrivia();
        return leading.FirstOrDefault().IsKind(SyntaxKind.EndOfLineTrivia)
            ? member
            : member.WithLeadingTrivia(leading.Insert(0, newLine));
    }
}
