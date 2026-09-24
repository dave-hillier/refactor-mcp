using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Simplification;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;

namespace RefactorMCP.ConsoleApp.Tools.Generators;

[McpServerToolType]
public static class ReplaceTypeCodeWithEnumTool
{
    [McpServerTool, Description("Replace int or string constants used as a type code with an enum in a new file beside the type. " +
        "Every reference to a constant becomes the enum member, and every field, property, parameter, local and return type " +
        "the codes flow into is retyped to the enum.")]
    public static async Task<string> ReplaceTypeCodeWithEnum(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the constants")] string filePath,
        [Description("Name of the type declaring the constants")] string typeName,
        [Description("Names of the constants that make up the type code, in the order the enum lists them")] string[] constantNames,
        [Description("Name of the enum to create")] string enumName,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var (type, declaration) = await TypeRefactoringHelpers.FindTypeAsync(document, typeName, cancellationToken);
        if (!SyntaxFacts.IsValidIdentifier(enumName))
            throw new McpException($"Error: '{enumName}' is not a valid type name");

        var constants = FindConstants(type, constantNames);
        var enumPath = Path.Combine(Path.GetDirectoryName(document.FilePath!)!, enumName + ".cs");
        if (type.ContainingNamespace.GetTypeMembers(enumName).Length > 0 || File.Exists(enumPath))
            throw new McpException($"Error: A type or file named {enumName} already exists in {type.ContainingNamespace.ToDisplayString()}");

        var enumDocument = MovingSupport.AddDocument(
            document.Project,
            enumPath,
            CSharpSyntaxTree.ParseText(EnumSource(declaration, constants, enumName)).GetRoot(cancellationToken));
        var updated = await TypeCodeEnum.ReplaceAsync(enumDocument.Project.Solution, constants, enumName, cancellationToken);

        var errors = await TypeRefactoringHelpers.NewErrorsAsync(solution, updated, cancellationToken);
        if (errors.Count > 0)
            throw new McpException($"Error: Replacing the type code with {enumName} would not compile: {TypeRefactoringHelpers.Describe(errors)}");

        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully replaced the type code of {typeName} with the enum {enumName} in {enumPath}";
    }

    private static List<IFieldSymbol> FindConstants(INamedTypeSymbol type, IReadOnlyList<string> names)
    {
        if (names.Count == 0)
            throw new McpException("Error: Name at least one constant");

        var constants = new List<IFieldSymbol>();
        foreach (var name in names)
        {
            var constant = type.GetMembers(name).OfType<IFieldSymbol>().FirstOrDefault(f => f.IsConst)
                ?? throw new McpException($"Error: {type.Name} has no constant named '{name}'");
            if (constant.Type.SpecialType is not (SpecialType.System_Int32 or SpecialType.System_String))
                throw new McpException($"Error: {name} is not an int or string constant, so it cannot be a type code");
            constants.Add(constant);
        }

        if (constants.Select(c => c.Type.SpecialType).Distinct().Count() > 1)
            throw new McpException("Error: The constants mix int and string values; a type code is one or the other");

        return constants;
    }

    /// <summary>
    /// The enum's file: the namespace of the type's file in the same style,
    /// the members in the order named, each with the comments above its
    /// constant. Values are written only when they are not 0, 1, 2 and so on.
    /// </summary>
    private static string EnumSource(TypeDeclarationSyntax declaration, IReadOnlyList<IFieldSymbol> constants, string enumName)
    {
        var eol = TypeRefactoringHelpers.EndOfLine(declaration.SyntaxTree.GetRoot()).ToString();
        var namespaceDeclaration = declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().LastOrDefault();
        var indent = namespaceDeclaration is NamespaceDeclarationSyntax ? "    " : "";
        var values = constants.Select(c => c.ConstantValue).ToList();
        var explicitValues = values.Any(v => v is int) && !values.Select((v, i) => (int)v! == i).All(same => same);

        var text = new StringBuilder();
        if (namespaceDeclaration is FileScopedNamespaceDeclarationSyntax fileScoped)
            text.Append($"namespace {fileScoped.Name};{eol}{eol}");
        else if (namespaceDeclaration is NamespaceDeclarationSyntax block)
            text.Append($"namespace {block.Name}{eol}{{{eol}");

        text.Append($"{indent}public enum {enumName}{eol}{indent}{{{eol}");
        for (var i = 0; i < constants.Count; i++)
        {
            var (comments, blankLineBefore) = CommentsAbove(constants[i]);
            if (i > 0 && blankLineBefore)
                text.Append(eol);
            foreach (var comment in comments)
                text.Append($"{indent}    {comment}{eol}");

            text.Append($"{indent}    {constants[i].Name}");
            if (explicitValues)
                text.Append($" = {constants[i].ConstantValue}");
            text.Append(i < constants.Count - 1 ? "," + eol : eol);
        }

        text.Append($"{indent}}}{eol}");
        if (namespaceDeclaration is NamespaceDeclarationSyntax)
            text.Append($"}}{eol}");
        return text.ToString();
    }

    /// <summary>The comment lines above a constant's declaration, and whether a blank line set it apart.</summary>
    private static (IReadOnlyList<string> Comments, bool BlankLineBefore) CommentsAbove(IFieldSymbol constant)
    {
        var variable = (VariableDeclaratorSyntax)constant.DeclaringSyntaxReferences[0].GetSyntax();
        var field = (FieldDeclarationSyntax)variable.Parent!.Parent!;
        if (field.Declaration.Variables[0] != variable)
            return (Array.Empty<string>(), false);

        var lines = field.GetLeadingTrivia().ToFullString().Replace("\r", "").Split('\n');
        var blankLineBefore = lines.Length > 1 && string.IsNullOrWhiteSpace(lines[0]);
        return (lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList(), blankLineBefore);
    }
}

/// <summary>
/// Finds where a type code flows and rewrites it to an enum: the constants'
/// references become enum members, and every declaration that carries a code
/// takes the enum's type.
/// </summary>
internal static class TypeCodeEnum
{
    private static readonly SyntaxAnnotation RemovedConstant = new("TypeCodeEnum.Removed");

    public static async Task<Solution> ReplaceAsync(
        Solution solution,
        IReadOnlyList<IFieldSymbol> originalConstants,
        string enumName,
        CancellationToken cancellationToken)
    {
        var constants = new List<IFieldSymbol>();
        foreach (var constant in originalConstants)
            constants.Add(await SolutionEdits.ResolveAsync(solution, SolutionEdits.ProjectOf(solution, constant), constant, cancellationToken));

        var codeType = constants[0].Type;
        var qualifiedEnum = QualifiedEnumName(constants[0].ContainingType, enumName);
        var edits = new SyntaxEdits();

        foreach (var carrier in await CarriersAsync(solution, constants, codeType, cancellationToken))
        {
            foreach (var reference in carrier.DeclaringSyntaxReferences)
            {
                var typeSyntax = DeclaredType(await reference.GetSyntaxAsync(cancellationToken));
                if (typeSyntax is null || typeSyntax.IsVar)
                    continue;

                edits.Replace(solution, typeSyntax, rewritten =>
                {
                    TypeSyntax enumType = SyntaxFactory.ParseTypeName(qualifiedEnum);
                    if (rewritten is NullableTypeSyntax)
                        enumType = SyntaxFactory.NullableType(enumType);
                    return enumType.WithTriviaFrom(rewritten).WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation);
                });
            }
        }

        foreach (var constant in constants)
        {
            foreach (var reference in await MemberReferences.FindAsync(solution, constant, cancellationToken))
            {
                edits.Replace(solution, reference.Callee, rewritten =>
                    SyntaxFactory.ParseExpression($"{qualifiedEnum}.{constant.Name}")
                        .WithTriviaFrom(rewritten)
                        .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation));
            }

            var variable = (VariableDeclaratorSyntax)await constant.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
            edits.Replace(solution, variable, rewritten => rewritten.WithAdditionalAnnotations(RemovedConstant));
        }

        var updated = await edits.ApplyAsync(solution, cancellationToken);
        var declaringDocument = solution.GetDocument(constants[0].DeclaringSyntaxReferences[0].SyntaxTree)!.Id;
        var root = await updated.GetDocument(declaringDocument)!.GetSyntaxRootAsync(cancellationToken);
        return updated.WithDocumentSyntaxRoot(declaringDocument, RemoveConstants(root!));
    }

    private static string QualifiedEnumName(INamedTypeSymbol type, string enumName) =>
        type.ContainingNamespace.IsGlobalNamespace
            ? $"global::{enumName}"
            : $"global::{type.ContainingNamespace.ToDisplayString()}.{enumName}";

    /// <summary>
    /// Removes the marked constants: a declaration of nothing else goes with
    /// its comments, which the enum members carry now.
    /// </summary>
    private static SyntaxNode RemoveConstants(SyntaxNode root)
    {
        while (root.GetAnnotatedNodes(RemovedConstant).FirstOrDefault() is VariableDeclaratorSyntax variable)
        {
            var field = (FieldDeclarationSyntax)variable.Parent!.Parent!;
            if (field.Declaration.Variables.Count > 1)
            {
                root = root.ReplaceNode(field.Declaration, field.Declaration.WithVariables(field.Declaration.Variables.Remove(variable)));
                continue;
            }

            var type = (TypeDeclarationSyntax)field.Parent!;
            root = root.ReplaceNode(type, type.WithMembers(MemberLayout.Remove(type.Members, field)));
        }

        return root;
    }

    /// <summary>The type written in a declaration of a field, local, property, parameter or method.</summary>
    private static TypeSyntax? DeclaredType(SyntaxNode declaration) => declaration switch
    {
        VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax variables } => variables.Type,
        PropertyDeclarationSyntax property => property.Type,
        ParameterSyntax parameter => parameter.Type,
        MethodDeclarationSyntax method => method.ReturnType,
        LocalFunctionStatementSyntax local => local.ReturnType,
        _ => null,
    };

    /// <summary>
    /// The declarations that hold a code: those a constant is assigned,
    /// passed, compared or switched against, then, until nothing changes,
    /// those that exchange values with a declaration already found.
    /// </summary>
    private static async Task<IReadOnlyCollection<ISymbol>> CarriersAsync(
        Solution solution,
        IReadOnlyList<IFieldSymbol> constants,
        ITypeSymbol codeType,
        CancellationToken cancellationToken)
    {
        var flows = new CodeFlows(constants, codeType);
        foreach (var project in solution.Projects)
        {
            foreach (var document in TypeRefactoringHelpers.SourceDocuments(project))
            {
                var root = await document.GetSyntaxRootAsync(cancellationToken);
                var model = await document.GetSemanticModelAsync(cancellationToken);
                flows.Collect(root!, model!);
            }
        }

        return flows.Carriers();
    }

    private sealed class CodeFlows
    {
        private readonly IReadOnlyList<IFieldSymbol> _constants;
        private readonly ITypeSymbol _codeType;
        private readonly HashSet<ISymbol> _seeds = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<ISymbol, HashSet<ISymbol>> _links = new(SymbolEqualityComparer.Default);
        private SemanticModel _model = null!;

        public CodeFlows(IReadOnlyList<IFieldSymbol> constants, ITypeSymbol codeType)
        {
            _constants = constants;
            _codeType = codeType;
        }

        public void Collect(SyntaxNode root, SemanticModel model)
        {
            _model = model;
            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case AssignmentExpressionSyntax assignment when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression):
                        Link(Slot(assignment.Left), Slot(assignment.Right));
                        break;
                    case VariableDeclaratorSyntax { Initializer: { } initializer } variable:
                        Link(Declared(variable), Slot(initializer.Value));
                        break;
                    case PropertyDeclarationSyntax { Initializer: { } initializer } property:
                        Link(Declared(property), Slot(initializer.Value));
                        break;
                    case ArgumentSyntax argument when model.GetOperation(argument) is IArgumentOperation { Parameter: { } parameter }:
                        Link(parameter, Slot(argument.Expression));
                        break;
                    case BinaryExpressionSyntax binary when binary.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression:
                        Link(Slot(binary.Left), Slot(binary.Right));
                        break;
                    case ReturnStatementSyntax { Expression: { } returned }:
                        Link(ReturningMember(node), Slot(returned));
                        break;
                    case ArrowExpressionClauseSyntax arrow:
                        Link(ReturningMember(node), Slot(arrow.Expression));
                        break;
                    case SwitchStatementSyntax switchStatement:
                        foreach (var label in switchStatement.Sections.SelectMany(s => s.Labels))
                        {
                            if (label is CaseSwitchLabelSyntax caseLabel)
                                Link(Slot(switchStatement.Expression), Slot(caseLabel.Value));
                            else if (label is CasePatternSwitchLabelSyntax { Pattern: ConstantPatternSyntax constant })
                                Link(Slot(switchStatement.Expression), Slot(constant.Expression));
                        }
                        break;
                    case SwitchExpressionSyntax switchExpression:
                        foreach (var arm in switchExpression.Arms)
                        {
                            if (arm.Pattern is ConstantPatternSyntax constant)
                                Link(Slot(switchExpression.GoverningExpression), Slot(constant.Expression));
                        }
                        break;
                    case IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax constant } isPattern:
                        Link(Slot(isPattern.Expression), Slot(constant.Expression));
                        break;
                }
            }
        }

        public IReadOnlyCollection<ISymbol> Carriers()
        {
            var found = new HashSet<ISymbol>(_seeds, SymbolEqualityComparer.Default);
            var pending = new Queue<ISymbol>(_seeds);
            while (pending.Count > 0)
            {
                var symbol = pending.Dequeue();
                if (!_links.TryGetValue(symbol, out var linked))
                    continue;

                foreach (var next in linked.Where(found.Add))
                    pending.Enqueue(next);
            }

            return found;
        }

        /// <summary>
        /// What an expression is in terms of the flow: a code, a declaration
        /// that could carry one, or neither.
        /// </summary>
        private Slot? Slot(ExpressionSyntax expression)
        {
            while (expression is ParenthesizedExpressionSyntax parenthesized)
                expression = parenthesized.Expression;

            var symbol = _model.GetSymbolInfo(expression).Symbol;
            if (symbol is IFieldSymbol field && _constants.Contains(field.OriginalDefinition, SymbolEqualityComparer.Default))
                return new Slot(null, IsCode: true);

            return symbol switch
            {
                IFieldSymbol or IPropertySymbol or ILocalSymbol or IParameterSymbol => Declared(symbol),
                IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.LocalFunction } method when expression is InvocationExpressionSyntax => Declared(method),
                _ => null,
            };
        }

        private Slot? Declared(SyntaxNode declaration) => Declared(_model.GetDeclaredSymbol(declaration));

        private Slot? Declared(ISymbol? symbol)
        {
            if (symbol is null || !symbol.Locations.Any(l => l.IsInSource))
                return null;
            if (symbol is IFieldSymbol { IsConst: true } constant && _constants.Contains(constant.OriginalDefinition, SymbolEqualityComparer.Default))
                return new Slot(null, IsCode: true);

            var type = symbol switch
            {
                IFieldSymbol f => f.Type,
                IPropertySymbol p => p.Type,
                ILocalSymbol l => l.Type,
                IParameterSymbol p => p.Type,
                IMethodSymbol m => m.ReturnType,
                _ => null,
            };
            return SymbolEqualityComparer.Default.Equals(type, _codeType) ? new Slot(symbol.OriginalDefinition, IsCode: false) : null;
        }

        private Slot? ReturningMember(SyntaxNode node)
        {
            var owner = node.Ancestors().FirstOrDefault(a => a is AnonymousFunctionExpressionSyntax
                or LocalFunctionStatementSyntax or MethodDeclarationSyntax or PropertyDeclarationSyntax
                or AccessorDeclarationSyntax or BaseMethodDeclarationSyntax);
            return owner switch
            {
                MethodDeclarationSyntax or LocalFunctionStatementSyntax or PropertyDeclarationSyntax => Declared(owner),
                AccessorDeclarationSyntax { Parent.Parent: PropertyDeclarationSyntax property } accessor
                    when accessor.IsKind(SyntaxKind.GetAccessorDeclaration) => Declared(property),
                _ => null,
            };
        }

        private void Link(ISymbol? parameter, Slot? other) =>
            Link(parameter is null ? null : Declared(parameter), other);

        private void Link(Slot? left, Slot? right)
        {
            if (left is null || right is null)
                return;

            if (left.IsCode && right.Symbol is not null)
                _seeds.Add(right.Symbol);
            else if (right.IsCode && left.Symbol is not null)
                _seeds.Add(left.Symbol);
            else if (left.Symbol is not null && right.Symbol is not null)
            {
                Linked(left.Symbol).Add(right.Symbol);
                Linked(right.Symbol).Add(left.Symbol);
            }
        }

        private HashSet<ISymbol> Linked(ISymbol symbol)
        {
            if (!_links.TryGetValue(symbol, out var linked))
                _links[symbol] = linked = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            return linked;
        }
    }

    private sealed record Slot(ISymbol? Symbol, bool IsCode);
}
