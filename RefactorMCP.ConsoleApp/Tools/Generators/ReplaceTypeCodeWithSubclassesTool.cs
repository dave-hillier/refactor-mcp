using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;

namespace RefactorMCP.ConsoleApp.Tools.Generators;

[McpServerToolType]
public static class ReplaceTypeCodeWithSubclassesTool
{
    [McpServerTool, Description("Replace an enum type code field with a sealed subclass per enum member. The class becomes " +
        "abstract, the field becomes an abstract property each subclass overrides with its member, the constructor loses " +
        "the code parameter, and a static Create factory maps a code to its subclass. Constructions that pass a known " +
        "code construct the subclass; the rest call the factory.")]
    public static async Task<string> ReplaceTypeCodeWithSubclasses(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the field")] string filePath,
        [Description("Name of the enum field holding the type code")] string fieldName,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var field = (IFieldSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, fieldName, null, s => s is IFieldSymbol, "field", cancellationToken);

        var updated = await TypeCodeSubclasses.ReplaceAsync(solution, field, cancellationToken);
        var errors = await TypeRefactoringHelpers.NewErrorsAsync(solution, updated, cancellationToken);
        if (errors.Count > 0)
            throw new McpException($"Error: Replacing {fieldName} with subclasses would not compile: {TypeRefactoringHelpers.Describe(errors)}");

        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully replaced the type code {fieldName} of {field.ContainingType.Name} with subclasses";
    }
}

/// <summary>
/// Turns a class with an enum type code into an abstract class with a sealed
/// subclass per enum member.
/// </summary>
internal static class TypeCodeSubclasses
{
    private static readonly SyntaxAnnotation ClassMark = new("TypeCodeSubclasses.Class");
    private static readonly SyntaxAnnotation ConstructorMark = new("TypeCodeSubclasses.Constructor");
    private static readonly SyntaxAnnotation RemovedMark = new("TypeCodeSubclasses.Removed");

    /// <summary>The single constructor that sets the code, and the parameter it sets it from.</summary>
    private sealed record CodeConstructor(
        IMethodSymbol Symbol,
        ConstructorDeclarationSyntax Declaration,
        ExpressionStatementSyntax Assignment,
        IParameterSymbol Parameter);

    public static async Task<Solution> ReplaceAsync(Solution solution, IFieldSymbol field, CancellationToken cancellationToken)
    {
        var type = field.ContainingType;
        if (field.Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            throw new McpException($"Error: {field.Name} is of type {field.Type.ToDisplayString()}, not an enum; replace the type code with an enum first");
        if (type.TypeKind != TypeKind.Class || type.IsSealed || type.IsStatic || type.IsRecord)
            throw new McpException($"Error: {type.Name} is not a class that can have subclasses");

        var members = enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).ToList();
        foreach (var member in members)
        {
            if (type.ContainingNamespace.GetTypeMembers(member.Name).Length > 0 || type.ContainingType?.GetTypeMembers(member.Name).Length > 0)
                throw new McpException($"Error: A type named {member.Name} already exists in {type.ContainingNamespace.ToDisplayString()}");
        }

        var references = await MemberReferences.FindAsync(solution, field, cancellationToken);
        var constructor = await CodeConstructorAsync(type, field, references, cancellationToken);
        var variable = (VariableDeclaratorSyntax)await field.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var fieldDeclaration = (FieldDeclarationSyntax)variable.Parent!.Parent!;
        var classDeclaration = (ClassDeclarationSyntax)fieldDeclaration.Parent!;
        if (fieldDeclaration.Declaration.Variables.Count > 1)
            throw new McpException($"Error: {field.Name} is declared together with other fields; declare it on its own first");

        var reused = await ReturningPropertyAsync(type, field, cancellationToken);
        var propertyName = reused?.Identifier.ValueText ?? PropertyName(field.Name);
        var enumSyntax = fieldDeclaration.Declaration.Type.WithoutTrivia().ToString();
        var access = reused is not null
            ? string.Join(" ", reused.Modifiers.Where(m => SyntaxFacts.IsAccessibilityModifier(m.Kind())).Select(m => m.Text))
            : field.DeclaredAccessibility == Accessibility.Private
                ? "protected"
                : string.Join(" ", fieldDeclaration.Modifiers.Where(m => SyntaxFacts.IsAccessibilityModifier(m.Kind())).Select(m => m.Text));

        var edits = new SyntaxEdits();
        var documentId = solution.GetDocument(classDeclaration.SyntaxTree)!.Id;

        foreach (var reference in references)
        {
            if (reference.Name.Ancestors().Contains(constructor.Assignment) || (reused is not null && reference.Name.Ancestors().Contains(reused)))
                continue;
            edits.Replace(reference.Document.Id, reference.Name, rewritten =>
                SyntaxFactory.IdentifierName(propertyName).WithTriviaFrom(rewritten));
        }

        if (reused is null)
        {
            edits.Replace(documentId, fieldDeclaration, rewritten =>
                Member($"{access} abstract {enumSyntax} {propertyName} {{ get; }}")
                    .WithLeadingTrivia(rewritten.GetLeadingTrivia())
                    .WithTrailingTrivia(rewritten.GetTrailingTrivia()));
        }
        else
        {
            edits.Replace(documentId, fieldDeclaration, rewritten => rewritten.WithAdditionalAnnotations(RemovedMark));
            edits.Replace(documentId, reused, rewritten =>
                Member($"{string.Join(" ", reused.Modifiers.Select(m => m.Text))} abstract {reused.Type.WithoutTrivia()} {propertyName} {{ get; }}")
                    .WithLeadingTrivia(rewritten.GetLeadingTrivia())
                    .WithTrailingTrivia(rewritten.GetTrailingTrivia()));
        }

        var baseName = classDeclaration.Identifier.ValueText + (classDeclaration.TypeParameterList?.WithoutTrivia().ToString() ?? "");
        var remaining = constructor.Declaration.ParameterList.Parameters
            .Where(p => p.Identifier.ValueText != constructor.Parameter.Name)
            .ToList();
        var factory = Factory(baseName, enumSyntax, members, constructor, remaining);

        edits.Replace(documentId, constructor.Assignment, rewritten => rewritten.WithAdditionalAnnotations(RemovedMark));
        edits.Replace(documentId, constructor.Declaration, rewritten =>
        {
            var declaration = (ConstructorDeclarationSyntax)rewritten;
            if (remaining.Count == 0 && declaration.Initializer is null && declaration.Body?.Statements.Count == 1)
            {
                return factory
                    .WithLeadingTrivia(MemberLayout.LeadingBlankLines(declaration.GetLeadingTrivia()))
                    .WithTrailingTrivia(declaration.GetTrailingTrivia());
            }

            declaration = declaration.WithParameterList(declaration.ParameterList.WithParameters(
                SyntaxFactory.SeparatedList(remaining.Select(p => p.WithoutTrivia()), Enumerable.Repeat(
                    SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space), Math.Max(0, remaining.Count - 1)))));
            declaration = HierarchyMemberHelpers.WithModifiers(declaration, modifiers => Protected(modifiers));
            return declaration.WithAdditionalAnnotations(ConstructorMark);
        });

        edits.Replace(documentId, classDeclaration, rewritten =>
        {
            var declaration = (ClassDeclarationSyntax)rewritten;
            var list = declaration.Members;
            if (list.FirstOrDefault(m => m.HasAnnotation(ConstructorMark)) is { } kept)
            {
                list = list.Insert(list.IndexOf(kept) + 1, factory
                    .WithLeadingTrivia(SyntaxFactory.EndOfLine("\n"))
                    .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n")));
            }

            if (list.FirstOrDefault(m => m.HasAnnotation(RemovedMark)) is { } removed)
                list = MemberLayout.Remove(list, removed);

            declaration = RemoveAssignment(declaration.WithMembers(list));
            declaration = HierarchyMemberHelpers.WithModifiers(declaration, modifiers =>
                HierarchyMemberHelpers.WithModifier(modifiers, SyntaxKind.AbstractKeyword));
            return declaration.WithAdditionalAnnotations(ClassMark);
        });

        var subclasses = members.Select(m => Subclass(classDeclaration, baseName, enumSyntax, m, remaining, access, propertyName)).ToList();
        var container = classDeclaration.Parent!;
        edits.Replace(documentId, container, rewritten =>
        {
            var declared = rewritten.GetAnnotatedNodes(ClassMark).OfType<MemberDeclarationSyntax>().Single();
            return InsertAfter(rewritten, declared, subclasses);
        });

        await RetargetConstructionsAsync(solution, constructor, members, edits, cancellationToken);
        return await edits.ApplyAsync(solution, cancellationToken);
    }

    private static async Task<CodeConstructor> CodeConstructorAsync(
        INamedTypeSymbol type,
        IFieldSymbol field,
        IReadOnlyList<MemberReference> references,
        CancellationToken cancellationToken)
    {
        foreach (var reference in references.Where(r => IsWrite(r.Callee)))
        {
            var owner = reference.Model.GetEnclosingSymbol(reference.Name.SpanStart, cancellationToken);
            if (owner is not IMethodSymbol { MethodKind: MethodKind.Constructor } || !SymbolEqualityComparer.Default.Equals(owner.ContainingType, type))
                throw new McpException($"Error: {field.Name} is assigned in {owner?.Name ?? "an initializer"}, so the code changes after construction and cannot be a subclass");
        }

        var failure = new McpException($"Error: {type.Name} must have one constructor that assigns {field.Name} from one of its parameters");
        var constructors = type.InstanceConstructors.Where(c => !c.IsImplicitlyDeclared).ToList();
        if (constructors.Count != 1 || field.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is VariableDeclaratorSyntax { Initializer: not null })
            throw failure;

        var declaration = (ConstructorDeclarationSyntax)await constructors[0].DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var assignments = references
            .Where(r => IsWrite(r.Callee) && r.Name.Ancestors().Contains(declaration))
            .Select(r => (Reference: r, Statement: r.Callee.Parent?.Parent as ExpressionStatementSyntax))
            .ToList();
        if (assignments.Count != 1 || assignments[0].Statement is not { Parent: BlockSyntax } statement
            || declaration.Initializer is { ThisOrBaseKeyword.RawKind: (int)SyntaxKind.ThisKeyword })
            throw failure;

        var value = ((AssignmentExpressionSyntax)statement.Expression).Right;
        if (assignments[0].Reference.Model.GetSymbolInfo(value, cancellationToken).Symbol is not IParameterSymbol parameter
            || !SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, constructors[0]))
            throw failure;

        return new CodeConstructor(constructors[0], declaration, statement, parameter);
    }

    private static bool IsWrite(ExpressionSyntax callee) => callee.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == callee,
        PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => true,
        ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None),
        _ => false,
    };

    /// <summary>A get-only property of the class that does nothing but return the field.</summary>
    private static async Task<PropertyDeclarationSyntax?> ReturningPropertyAsync(INamedTypeSymbol type, IFieldSymbol field, CancellationToken cancellationToken)
    {
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>().Where(p => p.SetMethod is null && !p.IsStatic))
        {
            var reference = property.DeclaringSyntaxReferences.FirstOrDefault();
            if (reference is null || await reference.GetSyntaxAsync(cancellationToken) is not PropertyDeclarationSyntax declaration)
                continue;

            var returned = declaration.ExpressionBody?.Expression
                ?? declaration.AccessorList?.Accessors.SingleOrDefault()?.ExpressionBody?.Expression
                ?? (declaration.AccessorList?.Accessors.SingleOrDefault()?.Body?.Statements.SingleOrDefault() as ReturnStatementSyntax)?.Expression;
            var name = returned switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var member } => member.Identifier.ValueText,
                _ => null,
            };
            if (name == field.Name && SymbolEqualityComparer.Default.Equals(property.Type, field.Type))
                return declaration;
        }

        return null;
    }

    private static string PropertyName(string fieldName)
    {
        var name = fieldName.TrimStart('_');
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static SyntaxTokenList Protected(SyntaxTokenList modifiers)
    {
        var access = modifiers.Where(m => SyntaxFacts.IsAccessibilityModifier(m.Kind())).ToList();
        var rest = HierarchyMemberHelpers.WithoutModifiers(modifiers, access.Select(m => m.Kind()).ToArray());
        return rest.Insert(0, SyntaxFactory.Token(SyntaxKind.ProtectedKeyword).WithTrailingTrivia(SyntaxFactory.Space));
    }

    private static ClassDeclarationSyntax RemoveAssignment(ClassDeclarationSyntax declaration)
    {
        var statement = declaration.GetAnnotatedNodes(RemovedMark).OfType<StatementSyntax>().SingleOrDefault();
        return statement is null ? declaration : declaration.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia)!;
    }

    /// <summary><c>public static Base Create(...) =&gt; code switch { ... };</c>, taking the constructor's parameters.</summary>
    private static MemberDeclarationSyntax Factory(
        string baseName,
        string enumSyntax,
        IReadOnlyList<IFieldSymbol> members,
        CodeConstructor constructor,
        IReadOnlyList<ParameterSyntax> remaining)
    {
        var code = constructor.Parameter.Name;
        var typeArguments = baseName.Contains('<') ? baseName[baseName.IndexOf('<')..] : "";
        var arguments = string.Join(", ", remaining.Select(p => p.Identifier.Text));
        var arms = string.Concat(members.Select(m => $"    {enumSyntax}.{m.Name} => new {m.Name}{typeArguments}({arguments}),\n"));
        var parameters = string.Join(", ", constructor.Declaration.ParameterList.Parameters.Select(p => p.WithoutTrivia().ToString()));
        var text = $"public static {baseName} Create({parameters}) => {code} switch\n{{\n{arms}    _ => throw new global::System.ArgumentOutOfRangeException(nameof({code})),\n}};\n";
        var factory = Member(text);
        var exception = factory.DescendantNodes().OfType<QualifiedNameSyntax>().First(q => q.ToString().EndsWith("ArgumentOutOfRangeException", StringComparison.Ordinal));
        return factory.ReplaceNode(exception, exception.WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation));
    }

    private static MemberDeclarationSyntax Subclass(
        ClassDeclarationSyntax baseDeclaration,
        string baseName,
        string enumSyntax,
        IFieldSymbol member,
        IReadOnlyList<ParameterSyntax> remaining,
        string access,
        string propertyName)
    {
        var classAccess = string.Join(" ", baseDeclaration.Modifiers.Where(m => SyntaxFacts.IsAccessibilityModifier(m.Kind())).Select(m => m.Text));
        var typeParameters = baseDeclaration.TypeParameterList?.WithoutTrivia().ToString() ?? "";
        var constraints = string.Concat(baseDeclaration.ConstraintClauses.Select(c => " " + c.WithoutTrivia()));
        var body = new List<string>();
        if (remaining.Count > 0)
        {
            var parameters = string.Join(", ", remaining.Select(p => p.WithoutTrivia().ToString()));
            var arguments = string.Join(", ", remaining.Select(p => p.Identifier.Text));
            body.Add($"public {member.Name}({parameters}) : base({arguments})\n{{\n}}\n");
        }

        body.Add($"{access} override {enumSyntax} {propertyName} => {enumSyntax}.{member.Name};\n");
        var text = $"{(classAccess.Length > 0 ? classAccess + " " : "")}sealed class {member.Name}{typeParameters} : {baseName}{constraints}\n{{\n{string.Join("\n", body)}}}\n";
        return Member(text).WithLeadingTrivia(SyntaxFactory.EndOfLine("\n"));
    }

    private static MemberDeclarationSyntax Member(string text) =>
        SyntaxFactory.ParseMemberDeclaration(text)!.WithAdditionalAnnotations(Formatter.Annotation);

    private static SyntaxNode InsertAfter(SyntaxNode container, MemberDeclarationSyntax after, IEnumerable<MemberDeclarationSyntax> added)
    {
        SyntaxList<MemberDeclarationSyntax> Insert(SyntaxList<MemberDeclarationSyntax> list) =>
            list.InsertRange(list.IndexOf(after) + 1, added);

        return container switch
        {
            BaseNamespaceDeclarationSyntax ns => ns.WithMembers(Insert(ns.Members)),
            CompilationUnitSyntax unit => unit.WithMembers(Insert(unit.Members)),
            TypeDeclarationSyntax type => type.WithMembers(Insert(type.Members)),
            _ => throw new McpException($"Error: Cannot add subclasses beside {after.Kind()}"),
        };
    }

    /// <summary>
    /// <c>new Base(Code.Member, x)</c> becomes <c>new Member(x)</c>; a
    /// construction whose code is not a constant calls the factory.
    /// </summary>
    private static async Task RetargetConstructionsAsync(
        Solution solution,
        CodeConstructor constructor,
        IReadOnlyList<IFieldSymbol> members,
        SyntaxEdits edits,
        CancellationToken cancellationToken)
    {
        var references = await SymbolFinder.FindReferencesAsync(constructor.Symbol, solution, cancellationToken);
        foreach (var location in references.SelectMany(r => r.Locations).Where(l => l.Location.IsInSource))
        {
            var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
            var model = await location.Document.GetSemanticModelAsync(cancellationToken);
            var creation = root!.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true)
                .AncestorsAndSelf().OfType<ObjectCreationExpressionSyntax>().FirstOrDefault();
            if (creation?.ArgumentList is null)
                continue;

            var codeArgument = creation.ArgumentList.Arguments.FirstOrDefault(a =>
                model!.GetOperation(a, cancellationToken) is IArgumentOperation { Parameter: { } p }
                && p.Ordinal == constructor.Parameter.Ordinal);
            var constant = codeArgument is null ? default : model!.GetConstantValue(codeArgument.Expression, cancellationToken);
            var member = constant.HasValue ? members.FirstOrDefault(m => Equals(m.ConstantValue, constant.Value)) : null;

            string replacement;
            if (member is not null)
            {
                var typeArguments = creation.Type is GenericNameSyntax generic ? generic.TypeArgumentList.ToString() : "";
                var arguments = creation.ArgumentList.WithArguments(creation.ArgumentList.Arguments.Remove(codeArgument!));
                replacement = $"new {member.Name}{typeArguments}{arguments}";
            }
            else
            {
                replacement = $"{creation.Type.WithoutTrivia()}.Create{creation.ArgumentList}";
            }

            edits.Replace(location.Document.Id, creation, rewritten =>
                SyntaxFactory.ParseExpression(replacement).WithTriviaFrom(rewritten));
        }
    }
}
