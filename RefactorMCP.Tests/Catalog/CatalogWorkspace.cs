using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace RefactorMCP.Tests.Catalog;

/// <summary>
/// A temporary solution built from a case's <c>before/</c> files. The runner
/// generates the project and solution files, so fixtures never carry them.
///
/// The session is given an in-memory solution built by
/// <see cref="ProjectLayouts"/>, which restores and loads each distinct set of
/// project files through MSBuild only once per run. Set <c>CATALOG_MSBUILD=1</c>
/// to restore and load every case through MSBuild instead, as a client would.
/// </summary>
internal sealed class CatalogWorkspace : IDisposable
{
    private const string TargetFramework = "net9.0";
    private const string DefaultProjectName = "Catalog";

    private readonly Dictionary<string, SourceMarkers> _markers;
    private readonly Workspace? _workspace;

    private CatalogWorkspace(string root, string solutionPath, Dictionary<string, SourceMarkers> markers, Workspace? workspace)
    {
        Root = root;
        SolutionPath = solutionPath;
        _markers = markers;
        _workspace = workspace;
    }

    private static bool LoadEveryCaseThroughMsBuild => Environment.GetEnvironmentVariable("CATALOG_MSBUILD") == "1";

    public string Root { get; }

    public string SolutionPath { get; }

    public static async Task<CatalogWorkspace> CreateAsync(CatalogCase catalogCase)
    {
        var root = Path.Combine(Path.GetTempPath(), "refactor-catalog", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var markers = new Dictionary<string, SourceMarkers>(StringComparer.Ordinal);
        foreach (var relative in SourceFiles(catalogCase.BeforeDirectory))
        {
            var stripped = SourceMarkers.Strip(await File.ReadAllTextAsync(Path.Combine(catalogCase.BeforeDirectory, relative)));
            var destination = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllTextAsync(destination, stripped.Text);
            markers[relative] = stripped;
        }

        var projects = WriteProjects(root, catalogCase.Definition);
        var solutionPath = Path.Combine(root, "Catalog.sln");
        await File.WriteAllTextAsync(solutionPath, SolutionText(projects));

        if (LoadEveryCaseThroughMsBuild)
        {
            await RestoreAsync(solutionPath);
            return new CatalogWorkspace(root, solutionPath, markers, workspace: null);
        }

        var workspace = await ProjectLayouts.CreateWorkspaceAsync(root, solutionPath, projects);
        SessionRegistry.GetOrCreate(solutionPath).Replace(workspace.CurrentSolution);
        return new CatalogWorkspace(root, solutionPath, markers, workspace);
    }

    /// <summary>The markers found in a <c>before/</c> file, by its path relative to <c>before/</c>.</summary>
    public SourceMarkers MarkersFor(string relativePath) =>
        _markers.TryGetValue(Normalize(relativePath), out var markers)
            ? markers
            : throw new InvalidOperationException($"before/ has no file '{relativePath}'");

    public string PathFor(string relativePath) => Path.Combine(Root, relativePath);

    /// <summary>The source files now in the workspace, by path relative to its root.</summary>
    public IReadOnlyDictionary<string, string> ReadSources() =>
        SourceFiles(Root).ToDictionary(
            relative => relative,
            relative => File.ReadAllText(Path.Combine(Root, relative)),
            StringComparer.Ordinal);

    /// <summary>
    /// Compiles every project in the solution against the sources now on
    /// disk, so files the refactoring created or deleted are accounted for.
    /// </summary>
    public async Task<IReadOnlyList<Diagnostic>> CompileAsync(Solution loaded)
    {
        var solution = loaded;
        var sources = ReadSources();

        foreach (var project in loaded.Projects)
        {
            var projectDirectory = Path.GetDirectoryName(project.FilePath!)!;
            var updated = solution.GetProject(project.Id)!;
            foreach (var document in updated.Documents.Where(d => IsFixtureSource(d.FilePath)).ToList())
                updated = updated.RemoveDocument(document.Id);

            foreach (var (relative, text) in sources)
            {
                var path = Path.Combine(Root, relative);
                if (!IsUnder(path, projectDirectory) || !OwningProjectIs(path, projectDirectory, loaded))
                    continue;

                updated = updated.AddDocument(Path.GetFileName(path), SourceText.From(text, Encoding.UTF8), filePath: path).Project;
            }

            solution = updated.Solution;
        }

        var diagnostics = new List<Diagnostic>();
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            diagnostics.AddRange(compilation!.GetDiagnostics()
                .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        }

        return diagnostics;
    }

    public void Dispose()
    {
        SessionRegistry.Unload(SolutionPath);
        _workspace?.Dispose();

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is harmless.
        }
    }

    /// <summary>Source files under a directory, by path relative to it, skipping build output.</summary>
    public static IEnumerable<string> SourceFiles(string directory)
    {
        if (!Directory.Exists(directory))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(file => Normalize(Path.GetRelativePath(directory, file)))
            .Where(relative => !relative.Split('/').Any(part => part is "obj" or "bin"))
            .OrderBy(relative => relative, StringComparer.Ordinal);
    }

    private static string Normalize(string relativePath) => relativePath.Replace('\\', '/');

    /// <summary>A source file the fixture owns, as opposed to one the build generated under obj/.</summary>
    private bool IsFixtureSource(string? path) =>
        path is not null
        && IsUnder(path, Root)
        && !Normalize(Path.GetRelativePath(Root, path)).Split('/').Any(part => part is "obj" or "bin");

    private static bool IsUnder(string path, string directory) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>
    /// A file belongs to the project whose directory is the deepest one
    /// containing it, which keeps a single root project from claiming the
    /// files of projects nested below it.
    /// </summary>
    private static bool OwningProjectIs(string path, string projectDirectory, Solution solution)
    {
        var owner = solution.Projects
            .Select(p => Path.GetDirectoryName(p.FilePath!)!)
            .Where(dir => IsUnder(path, dir))
            .OrderByDescending(dir => dir.Length)
            .First();

        return string.Equals(owner, projectDirectory, StringComparison.Ordinal);
    }

    private static List<GeneratedProject> WriteProjects(string root, CaseDefinition definition)
    {
        if (definition.Projects is not { Count: > 0 } projects)
        {
            var text = ProjectText(definition.Project, references: null);
            File.WriteAllText(Path.Combine(root, $"{DefaultProjectName}.csproj"), text);
            return new List<GeneratedProject>
            {
                new(DefaultProjectName, $"{DefaultProjectName}.csproj", Guid.NewGuid(), text, Array.Empty<string>()),
            };
        }

        var generated = new List<GeneratedProject>();
        foreach (var project in projects)
        {
            var directory = Path.Combine(root, project.Name);
            Directory.CreateDirectory(directory);
            var referenced = project.References ?? new List<string>();
            var text = ProjectText(project.Settings ?? definition.Project, referenced.Select(r => $"../{r}/{r}.csproj").ToList());
            File.WriteAllText(Path.Combine(directory, $"{project.Name}.csproj"), text);
            generated.Add(new GeneratedProject(project.Name, $"{project.Name}/{project.Name}.csproj", Guid.NewGuid(), text, referenced));
        }

        return generated;
    }

    private static string ProjectText(ProjectSettings? settings, IReadOnlyList<string>? references)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        builder.AppendLine("  <PropertyGroup>");
        builder.AppendLine($"    <TargetFramework>{TargetFramework}</TargetFramework>");
        builder.AppendLine($"    <LangVersion>{settings?.LangVersion ?? "latest"}</LangVersion>");
        builder.AppendLine($"    <Nullable>{settings?.Nullable ?? "disable"}</Nullable>");
        builder.AppendLine("    <ImplicitUsings>disable</ImplicitUsings>");
        builder.AppendLine("    <EnableDefaultCompileItems>true</EnableDefaultCompileItems>");
        builder.AppendLine("  </PropertyGroup>");

        if (references is { Count: > 0 })
        {
            builder.AppendLine("  <ItemGroup>");
            foreach (var reference in references)
                builder.AppendLine($"    <ProjectReference Include=\"{reference}\" />");
            builder.AppendLine("  </ItemGroup>");
        }

        builder.AppendLine("</Project>");
        return builder.ToString();
    }

    /// <summary>
    /// A solution with its configuration sections. Without them the workspace
    /// loads the projects' documents but resolves no metadata references.
    /// </summary>
    internal static string SolutionText(IReadOnlyList<GeneratedProject> projects)
    {
        const string csharpProjectType = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        var builder = new StringBuilder();
        builder.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        builder.AppendLine("# Visual Studio Version 17");
        foreach (var project in projects)
        {
            builder.AppendLine($"Project(\"{csharpProjectType}\") = \"{project.Name}\", \"{project.RelativePath.Replace('/', '\\')}\", \"{Id(project)}\"");
            builder.AppendLine("EndProject");
        }
        builder.AppendLine("Global");
        builder.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        builder.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
        builder.AppendLine("\tEndGlobalSection");
        builder.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var project in projects)
        {
            builder.AppendLine($"\t\t{Id(project)}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            builder.AppendLine($"\t\t{Id(project)}.Debug|Any CPU.Build.0 = Debug|Any CPU");
        }
        builder.AppendLine("\tEndGlobalSection");
        builder.AppendLine("EndGlobal");
        return builder.ToString();

        static string Id(GeneratedProject project) => $"{{{project.Id.ToString().ToUpperInvariant()}}}";
    }

    internal static async Task RestoreAsync(string solutionPath)
    {
        // A single node: a solution of several projects otherwise starts worker
        // nodes that inherit the output pipes and can outlive the restore,
        // leaving the reads below waiting forever. Restoring projects one at a
        // time stops NuGet restoring a referenced project twice at once, once
        // through each spelling of the temp directory, which fails.
        var startInfo = new ProcessStartInfo("dotnet", $"restore \"{solutionPath}\" --verbosity quiet --disable-build-servers --disable-parallel -m:1")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(solutionPath)!,
        };

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"dotnet restore failed for the generated solution:\n{await output}\n{await error}");
    }
}

/// <summary>A generated project file: its name, path relative to the case root, text and the projects it references.</summary>
internal sealed record GeneratedProject(string Name, string RelativePath, Guid Id, string Text, IReadOnlyList<string> References);
