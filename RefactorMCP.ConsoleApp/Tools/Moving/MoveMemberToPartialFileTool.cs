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
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

[McpServerToolType]
public static class MoveMemberToPartialFileTool
{
    [McpServerTool, Description("Move a member of a partial type into the part declared in another file, " +
        "creating the file with a new part when it does not exist")]
    public static async Task<string> MoveMemberToPartialFile(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the member")] string filePath,
        [Description("Name of the member to move")] string memberName,
        [Description("Path to the file holding, or to hold, the other part of the type")] string targetFilePath,
        [Description("Line of the member's declaration (1-based, optional), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var member = await MovingSupport.FindDeclaredSymbolAsync(
            document, memberName, line, s => s.ContainingType is not null, "member", cancellationToken);

        var declaration = member.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax(cancellationToken))
            .First(node => node.SyntaxTree.FilePath == document.FilePath);
        var memberDeclaration = declaration as MemberDeclarationSyntax
            ?? declaration.FirstAncestorOrSelf<MemberDeclarationSyntax>()!;
        var typeDeclaration = (TypeDeclarationSyntax)memberDeclaration.Parent!;

        if (!typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            throw new McpException($"Error: {member.ContainingType.Name} is not partial, so it has no other part to move {memberName} to");

        targetFilePath = RefactoringHelpers.ResolvePath(targetFilePath)!;
        if (string.Equals(Path.GetFullPath(targetFilePath), Path.GetFullPath(document.FilePath!), StringComparison.Ordinal))
            throw new McpException($"Error: {memberName} is already in {Path.GetFileName(targetFilePath)}");

        var target = RefactoringHelpers.GetDocumentByPath(solution, targetFilePath);
        TypeDeclarationSyntax? targetPart = null;
        if (target is not null)
        {
            targetPart = member.ContainingType.DeclaringSyntaxReferences
                .Where(r => r.SyntaxTree.FilePath == target.FilePath)
                .Select(r => (TypeDeclarationSyntax)r.GetSyntax(cancellationToken))
                .FirstOrDefault()
                ?? throw new McpException($"Error: {Path.GetFileName(targetFilePath)} declares no part of {member.ContainingType.Name}");
        }
        else if (File.Exists(targetFilePath))
        {
            throw new McpException($"Error: {Path.GetFileName(targetFilePath)} declares no part of {member.ContainingType.Name}; it is not in the solution");
        }
        else if (member.ContainingType.ContainingType is not null)
        {
            throw new McpException($"Error: {member.ContainingType.Name} is nested; add a part of it to {Path.GetFileName(targetFilePath)} first");
        }

        var (remaining, moved) = Detach(typeDeclaration, memberDeclaration, declaration);
        var sourceRoot = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
        var unnecessaryInSource = await UsingTextsAsync(document, cancellationToken);
        var updated = solution.WithDocumentSyntaxRoot(document.Id, sourceRoot.ReplaceNode(typeDeclaration, remaining));

        DocumentId targetId;
        HashSet<string> unnecessaryInTarget;
        if (target is not null)
        {
            unnecessaryInTarget = await UsingTextsAsync(target, cancellationToken);
            var targetRoot = (CompilationUnitSyntax)(await target.GetSyntaxRootAsync(cancellationToken))!;
            targetRoot = targetRoot.ReplaceNode(targetPart!, MemberLayout.Append(targetPart!, moved));
            targetRoot = WithUsingsFrom(targetRoot, sourceRoot);
            updated = updated.WithDocumentSyntaxRoot(target.Id, targetRoot);
            targetId = target.Id;
        }
        else
        {
            unnecessaryInTarget = new HashSet<string>(StringComparer.Ordinal);
            var newRoot = NewPartFile(sourceRoot, typeDeclaration);
            var part = newRoot.DescendantNodes().OfType<TypeDeclarationSyntax>().Single();
            newRoot = newRoot.ReplaceNode(part, MemberLayout.Append(part, moved));
            var created = MovingSupport.AddDocument(updated.GetProject(document.Project.Id)!, targetFilePath, newRoot);
            targetId = created.Id;
            updated = created.Project.Solution;
        }

        updated = (await MovingSupport.TidyAsync(updated.GetDocument(targetId)!, cancellationToken)).Project.Solution;
        updated = await RemoveNewlyUnnecessaryUsingsAsync(updated, targetId, unnecessaryInTarget, cancellationToken);
        updated = await RemoveNewlyUnnecessaryUsingsAsync(updated, document.Id, unnecessaryInSource, cancellationToken);

        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully moved {memberName} to {Path.GetFileName(targetFilePath)}";
    }

    /// <summary>
    /// The type without the member, and the member as a declaration of its
    /// own. A field declared alongside others is split out of its declaration.
    /// </summary>
    private static (TypeDeclarationSyntax Remaining, MemberDeclarationSyntax Moved) Detach(
        TypeDeclarationSyntax type,
        MemberDeclarationSyntax member,
        SyntaxNode declaration)
    {
        if (declaration is VariableDeclaratorSyntax variable
            && member is BaseFieldDeclarationSyntax field
            && field.Declaration.Variables.Count > 1)
        {
            var moved = field
                .WithDeclaration(field.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(variable.WithoutTrivia())))
                .WithLeadingTrivia(SyntaxFactory.ElasticMarker);
            var kept = field.WithDeclaration(field.Declaration.WithVariables(field.Declaration.Variables.Remove(variable)));
            return (type.ReplaceNode(field, kept), moved);
        }

        return (type.WithMembers(MemberLayout.Remove(type.Members, member)), member);
    }

    /// <summary>
    /// A file holding an empty part of <paramref name="type"/>, with the
    /// source file's usings and the same form of namespace.
    /// </summary>
    private static CompilationUnitSyntax NewPartFile(CompilationUnitSyntax source, TypeDeclarationSyntax type)
    {
        var text = new StringBuilder();
        foreach (var directive in source.Usings)
            text.Append(directive.WithoutTrivia().ToFullString()).Append('\n');
        if (source.Usings.Count > 0)
            text.Append('\n');

        var keyword = type is RecordDeclarationSyntax { ClassOrStructKeyword.RawKind: not 0 } record
            ? $"record {record.ClassOrStructKeyword.Text}"
            : type.Keyword.Text;
        var header = $"{type.Modifiers.ToString()} {keyword} {type.Identifier.Text}{type.TypeParameterList}";

        switch (type.Parent)
        {
            case FileScopedNamespaceDeclarationSyntax ns:
                text.Append($"namespace {ns.Name};\n\n{header}\n{{\n}}\n");
                break;
            case NamespaceDeclarationSyntax ns:
                text.Append($"namespace {ns.Name}\n{{\n    {header}\n    {{\n    }}\n}}\n");
                break;
            default:
                text.Append($"{header}\n{{\n}}\n");
                break;
        }

        return SyntaxFactory.ParseCompilationUnit(text.ToString());
    }

    /// <summary>Adds the source file's usings that the target lacks; the unneeded ones are pruned later.</summary>
    private static CompilationUnitSyntax WithUsingsFrom(CompilationUnitSyntax target, CompilationUnitSyntax source)
    {
        var present = target.Usings.Select(u => u.WithoutTrivia().ToString()).ToHashSet(StringComparer.Ordinal);
        var missing = source.Usings
            .Where(u => !present.Contains(u.WithoutTrivia().ToString()))
            .Select(u => u.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.EndOfLine("\n")))
            .ToList();
        if (missing.Count == 0)
            return target;

        var usings = target.Usings.AddRange(missing)
            .OrderBy(u => u.Name?.ToString().StartsWith("System", StringComparison.Ordinal) == true ? 0 : 1)
            .ThenBy(u => u.Name?.ToString(), StringComparer.Ordinal)
            .ToList();
        var updated = target.WithUsings(SyntaxFactory.List(usings));
        if (target.Usings.Count == 0 && updated.Members.Count > 0)
        {
            var first = updated.Members[0];
            updated = updated.ReplaceNode(first, first.WithLeadingTrivia(first.GetLeadingTrivia().Insert(0, SyntaxFactory.EndOfLine("\n"))));
        }

        return updated;
    }

    private static async Task<HashSet<string>> UsingTextsAsync(Document document, CancellationToken cancellationToken) =>
        (await MemberLayout.UnnecessaryUsingsAsync(document, cancellationToken))
            .Select(u => u.WithoutTrivia().ToString())
            .ToHashSet(StringComparer.Ordinal);

    private static async Task<Solution> RemoveNewlyUnnecessaryUsingsAsync(
        Solution solution,
        DocumentId id,
        HashSet<string> unnecessaryBefore,
        CancellationToken cancellationToken)
    {
        var document = solution.GetDocument(id)!;
        var unnecessary = (await MemberLayout.UnnecessaryUsingsAsync(document, cancellationToken))
            .Where(u => !unnecessaryBefore.Contains(u.WithoutTrivia().ToString()))
            .ToList();
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
        return solution.WithDocumentSyntaxRoot(id, MemberLayout.RemoveUsings(root, unnecessary));
    }
}
