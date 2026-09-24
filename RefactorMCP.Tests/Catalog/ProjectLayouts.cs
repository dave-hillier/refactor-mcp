using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace RefactorMCP.Tests.Catalog;

/// <summary>
/// Builds a case's solution without restoring or loading it through MSBuild.
///
/// Cases with the same generated project files differ only in their sources,
/// so each distinct set of project files is restored and loaded through
/// MSBuild once per test run. What MSBuild resolved for it (references,
/// compiler and parse options, generated documents) is kept, and each case
/// gets an in-memory solution with those settings and its own documents.
/// </summary>
internal static class ProjectLayouts
{
    private static readonly Dictionary<string, Task<Layout>> Layouts = new(StringComparer.Ordinal);
    private static readonly object Gate = new();
    private static readonly string LayoutsRoot =
        Path.Combine(Path.GetTempPath(), "refactor-catalog", "layouts", Guid.NewGuid().ToString("N"));

    static ProjectLayouts()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                if (Directory.Exists(LayoutsRoot))
                    Directory.Delete(LayoutsRoot, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory is harmless.
            }
        };
    }

    /// <summary>
    /// An in-memory solution for the projects and sources under
    /// <paramref name="root"/>, with the settings MSBuild resolved for the
    /// same project files.
    /// </summary>
    public static async Task<AdhocWorkspace> CreateWorkspaceAsync(
        string root,
        string solutionPath,
        IReadOnlyList<GeneratedProject> projects)
    {
        var layout = await LayoutFor(projects);
        var ids = projects.ToDictionary(p => p.Name, _ => ProjectId.CreateNewId(), StringComparer.Ordinal);
        var directories = projects.ToDictionary(
            p => p.Name,
            p => Path.GetDirectoryName(Path.Combine(root, p.RelativePath))!,
            StringComparer.Ordinal);

        var sources = CatalogWorkspace.SourceFiles(root).Select(relative => Path.Combine(root, relative)).ToList();
        var infos = new List<ProjectInfo>();

        foreach (var project in projects)
        {
            var id = ids[project.Name];
            var template = layout.Projects[project.RelativePath];
            var directory = directories[project.Name];

            var documents = sources
                .Where(path => OwnerOf(path, directories) == project.Name)
                .Select(path => Document(id, path, directory))
                .Concat(template.GeneratedDocuments.Select(d => Document(id, d.Path, d.Text, folders: Array.Empty<string>())))
                .ToList();

            var configDocuments = template.AnalyzerConfigDocuments
                .Select(d => Document(id, d.Path, d.Text, folders: Array.Empty<string>()));

            var info = ProjectInfo.Create(
                    id,
                    VersionStamp.Create(),
                    project.Name,
                    template.AssemblyName,
                    LanguageNames.CSharp,
                    filePath: Path.Combine(root, project.RelativePath),
                    compilationOptions: template.CompilationOptions,
                    parseOptions: template.ParseOptions,
                    documents: documents,
                    projectReferences: project.References.Select(r => new ProjectReference(ids[r])),
                    metadataReferences: template.MetadataReferences,
                    analyzerReferences: template.AnalyzerReferences)
                .WithDefaultNamespace(template.DefaultNamespace)
                .WithAnalyzerConfigDocuments(configDocuments);

            infos.Add(info);
        }

        var workspace = new AdhocWorkspace();
        workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create(), solutionPath, infos));
        return workspace;
    }

    private static Task<Layout> LayoutFor(IReadOnlyList<GeneratedProject> projects)
    {
        var key = string.Join("\n", projects.Select(p => $"{p.RelativePath}\n{p.Text}"));

        lock (Gate)
        {
            if (!Layouts.TryGetValue(key, out var layout))
            {
                layout = LoadAsync(projects);
                Layouts.Add(key, layout);
            }

            return layout;
        }
    }

    /// <summary>
    /// Restores and loads the project files, with no sources, through MSBuild
    /// and keeps what it resolved. The workspace stays open for the rest of
    /// the run, because the references it produced read their metadata lazily.
    /// </summary>
    private static async Task<Layout> LoadAsync(IReadOnlyList<GeneratedProject> projects)
    {
        var directory = Path.Combine(LayoutsRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        foreach (var project in projects)
        {
            var path = Path.Combine(directory, project.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, project.Text);
        }

        var solutionPath = Path.Combine(directory, "Catalog.sln");
        await File.WriteAllTextAsync(solutionPath, CatalogWorkspace.SolutionText(projects));
        await CatalogWorkspace.RestoreAsync(solutionPath);

        var workspace = RefactoringHelpers.CreateWorkspace();
        var solution = await workspace.OpenSolutionAsync(solutionPath);

        var templates = new Dictionary<string, ProjectTemplate>(StringComparer.Ordinal);
        foreach (var project in solution.Projects)
        {
            var relative = Path.GetRelativePath(directory, project.FilePath!).Replace('\\', '/');
            if (!project.MetadataReferences.Any())
                throw new InvalidOperationException($"MSBuild resolved no references for the generated project {relative}");

            var generated = new List<TemplateDocument>();
            foreach (var document in project.Documents)
                generated.Add(new TemplateDocument(document.FilePath!, (await document.GetTextAsync()).ToString()));

            var configs = new List<TemplateDocument>();
            foreach (var document in project.AnalyzerConfigDocuments)
                configs.Add(new TemplateDocument(document.FilePath!, (await document.GetTextAsync()).ToString()));

            templates[relative] = new ProjectTemplate(
                project.AssemblyName,
                project.DefaultNamespace,
                project.CompilationOptions!,
                project.ParseOptions!,
                project.MetadataReferences.ToList(),
                project.AnalyzerReferences.ToList(),
                generated,
                configs);
        }

        return new Layout(workspace, templates);
    }

    /// <summary>The project whose directory is the deepest one containing the file.</summary>
    private static string OwnerOf(string path, IReadOnlyDictionary<string, string> directories) =>
        directories
            .Where(pair => Path.GetFullPath(path).StartsWith(Path.GetFullPath(pair.Value) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderByDescending(pair => pair.Value.Length)
            .First()
            .Key;

    private static DocumentInfo Document(ProjectId project, string path, string projectDirectory)
    {
        var folders = Path.GetRelativePath(projectDirectory, Path.GetDirectoryName(path)!)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part != ".")
            .ToArray();

        return Document(project, path, File.ReadAllText(path), folders);
    }

    private static DocumentInfo Document(ProjectId project, string path, string text, IReadOnlyList<string> folders) =>
        DocumentInfo.Create(
            DocumentId.CreateNewId(project),
            Path.GetFileName(path),
            folders,
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text, Encoding.UTF8), VersionStamp.Create(), path)),
            filePath: path);

    private sealed record Layout(Workspace Workspace, IReadOnlyDictionary<string, ProjectTemplate> Projects);

    private sealed record ProjectTemplate(
        string AssemblyName,
        string? DefaultNamespace,
        CompilationOptions CompilationOptions,
        ParseOptions ParseOptions,
        IReadOnlyList<MetadataReference> MetadataReferences,
        IReadOnlyList<AnalyzerReference> AnalyzerReferences,
        IReadOnlyList<TemplateDocument> GeneratedDocuments,
        IReadOnlyList<TemplateDocument> AnalyzerConfigDocuments);

    private sealed record TemplateDocument(string Path, string Text);
}
