using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

/// <summary>
/// Moves a top-level type out of a file in a loaded solution into a file of
/// its own, named after it, beside the original.
/// </summary>
internal static class TypeFileMover
{
    public static async Task<string> MoveAsync(Document document, string typeName, CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;

        var declarations = root.DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .Where(node => DeclaredName(node) == typeName)
            .ToList();
        var type = declarations.FirstOrDefault(IsTopLevel);
        if (type is null)
        {
            throw declarations.Count > 0
                ? new McpException($"Error: Type {typeName} is nested in another type; only top-level types can move to their own file")
                : new McpException($"Error: Type {typeName} not found in {document.FilePath}");
        }

        if (RenameFileToMatchTypeTool.TopLevelTypeNames(root).Count == 1)
            throw new McpException($"Error: {typeName} is the only type in {Path.GetFileName(document.FilePath)}; rename the file to match the type instead");

        var newPath = Path.Combine(Path.GetDirectoryName(document.FilePath!)!, $"{typeName}.cs");
        if (File.Exists(newPath) || RefactoringHelpers.GetDocumentByPath(solution, newPath) is not null)
            throw new McpException($"Error: File {newPath} already exists");

        var unnecessaryBefore = UsingTexts(await MemberLayout.UnnecessaryUsingsAsync(document, cancellationToken));

        var sourceRoot = root.ReplaceNode(type.Parent!, WithoutMember(type.Parent!, type));
        var updated = solution.WithDocumentSyntaxRoot(document.Id, sourceRoot);
        var created = MovingSupport.AddDocument(updated.GetProject(document.Project.Id)!, newPath, OnlyType(type));
        updated = created.Project.Solution;

        created = updated.GetDocument(created.Id)!;
        var createdRoot = (CompilationUnitSyntax)(await created.GetSyntaxRootAsync(cancellationToken))!;
        createdRoot = MemberLayout.RemoveUsings(createdRoot, await MemberLayout.UnnecessaryUsingsAsync(created, cancellationToken));
        updated = updated.WithDocumentSyntaxRoot(created.Id, createdRoot);

        var source = updated.GetDocument(document.Id)!;
        var newlyUnnecessary = (await MemberLayout.UnnecessaryUsingsAsync(source, cancellationToken))
            .Where(u => !unnecessaryBefore.Contains(u.ToString()))
            .ToList();
        var tidiedSource = MemberLayout.RemoveUsings((CompilationUnitSyntax)(await source.GetSyntaxRootAsync(cancellationToken))!, newlyUnnecessary);
        updated = updated.WithDocumentSyntaxRoot(document.Id, tidiedSource);

        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully moved type '{typeName}' to {newPath}";
    }

    private static HashSet<string> UsingTexts(IEnumerable<UsingDirectiveSyntax> usings) =>
        usings.Select(u => u.ToString()).ToHashSet(StringComparer.Ordinal);

    private static string? DeclaredName(MemberDeclarationSyntax node) => node switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        DelegateDeclarationSyntax del => del.Identifier.ValueText,
        _ => null,
    };

    private static bool IsTopLevel(MemberDeclarationSyntax node) =>
        node.Parent is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax;

    private static SyntaxNode WithoutMember(SyntaxNode container, MemberDeclarationSyntax member) => container switch
    {
        CompilationUnitSyntax unit => unit.WithMembers(MemberLayout.Remove(unit.Members, member)),
        BaseNamespaceDeclarationSyntax ns => ns.WithMembers(MemberLayout.Remove(ns.Members, member)),
        _ => throw new McpException("Error: The type is not declared in a namespace or at the top of the file"),
    };

    /// <summary>
    /// The original file cut down to <paramref name="type"/> and the namespaces
    /// around it, keeping its header, usings and namespace form.
    /// </summary>
    private static CompilationUnitSyntax OnlyType(MemberDeclarationSyntax type)
    {
        MemberDeclarationSyntax kept = type;
        foreach (var container in type.Ancestors())
        {
            switch (container)
            {
                case NamespaceDeclarationSyntax ns:
                    kept = ns
                        .WithMembers(MemberLayout.KeepOnly(ns.Members, kept))
                        .WithCloseBraceToken(MemberLayout.WithoutDirectives(ns.CloseBraceToken));
                    break;
                case FileScopedNamespaceDeclarationSyntax ns:
                    kept = ns.WithMembers(MemberLayout.KeepOnly(ns.Members, kept));
                    break;
                case CompilationUnitSyntax unit:
                    // With no usings, directives above the first declaration
                    // are the file's header, such as #nullable, and are kept.
                    var isHeader = unit.Usings.Count == 0 && unit.Externs.Count == 0 && unit.Members.IndexOf(n => n.Span == kept.Span) == 0;
                    return unit
                        .WithAttributeLists(default)
                        .WithMembers(MemberLayout.KeepOnly(unit.Members, kept, keepDirectives: isHeader))
                        .WithEndOfFileToken(MemberLayout.WithoutDirectives(unit.EndOfFileToken));
            }
        }

        throw new McpException("Error: The type is not inside a compilation unit");
    }
}
