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
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

/// <summary>
/// Move one top-level type to an existing C# project, preserving bindings and transactionally
/// applying the changes. Failures retain full JSON evidence for Objectified DefectReport intake.
/// </summary>
[McpServerToolType]
public static class MoveTypeToProjectTool
{
    [McpServerTool, Description("Move a top-level C# type to another project, updating references, namespace and project dependencies. " +
        "Preflights compilation and cycles; filesystem changes are snapshotted and rolled back on failure. " +
        "Failures create verbose defect JSON and a Recorder submission command. Prefer dry-run first.")]
    public static async Task<string> MoveTypeToProject(
        [Description("Absolute path to the containing .sln or .slnx file")] string solutionPath,
        [Description("Source C# file containing the top-level type")] string filePath,
        [Description("Exact simple name of the type to move")] string typeName,
        [Description("Destination .csproj file in the same solution")] string targetProjectPath,
        [Description("Optional destination namespace; defaults to preserving the original namespace")] string? targetNamespace = null,
        [Description("Optional destination .cs file path, inside the target project; defaults to <TypeName>.cs in the project directory")] string? targetFilePath = null,
        [Description("Analyze and validate without writing changes; recommended for initial runs")] bool dryRun = false,
        [Description("Why the type is moving; captured in any failure report and its DefectReport")] string? purpose = null,
        [Description("Preserve binary clients by emitting a source-assembly type forwarder; requires a public non-generic type and unchanged namespace")] bool preserveBinaryCompatibility = false,
        [Description("Reject .Contracts projects depending directly or transitively on .Core projects (default true)")] bool enforceContractsBoundary = true,
        CancellationToken cancellationToken = default)
    {
        var trace = new TypeMoveTrace(solutionPath, filePath, typeName, targetProjectPath, targetNamespace, purpose)
        {
            TargetFilePath = targetFilePath
        };
        DiskSnapshot? snapshot = null;
        bool applyAttempted = false;
        SolutionSession? session = null;

        try
        {
            trace.Begin("load-solution", "Resolve solution and source/destination paths; load MSBuild workspace");
            solutionPath = Path.GetFullPath(solutionPath);
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            session = SessionRegistry.GetOrCreate(solutionPath);
            filePath = session.ResolvePath(filePath);
            targetProjectPath = session.ResolvePath(targetProjectPath);
            trace.Complete($"Loaded {solution.Projects.Count()} projects from {solutionPath}");

            trace.Begin("resolve-source-and-target", "Verify source project, destination .csproj, type declaration and destination path");
            var sourceDocument = MovingSupport.DocumentOrThrow(solution, filePath);
            var sourceProject = sourceDocument.Project;
            var destinationProject = solution.Projects.SingleOrDefault(project =>
                PathsEqual(project.FilePath, targetProjectPath))
                ?? throw new McpException($"Destination project {targetProjectPath} is absent from {solutionPath}");
            if (sourceProject.Id == destinationProject.Id)
                throw new McpException($"Source and destination are the same project: {sourceProject.Name}");
            if (sourceProject.Language != LanguageNames.CSharp || destinationProject.Language != LanguageNames.CSharp)
                throw new McpException("Both source and destination projects must be C#");
            if (solution.GetDocumentIdsWithFilePath(filePath).Count() != 1)
                throw new McpException($"Source file {filePath} is linked to multiple projects; manual resolution is required");

            if (string.IsNullOrWhiteSpace(typeName) || !SyntaxFacts.IsValidIdentifier(typeName))
                throw new McpException($"Invalid C# type name '{typeName}'");
            var type = (INamedTypeSymbol)await MovingSupport.FindDeclaredSymbolAsync(
                sourceDocument, typeName, line: null, s => s is INamedTypeSymbol,
                "top-level type", cancellationToken);
            if (type.ContainingType is not null)
                throw new McpException($"Nested type {type.Name} requires moving its containing type first");
            var parts = TypeMovePlanner.Parts(solution, type, sourceProject.Id);
            if (preserveBinaryCompatibility && (type.DeclaredAccessibility != Accessibility.Public || type.IsGenericType))
                throw new McpException($"Type forwarding requires a public non-generic top-level type: {type.ToDisplayString()}");

            var oldNamespace = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
            var resultingNamespace = targetNamespace ?? oldNamespace;
            if (!string.IsNullOrEmpty(resultingNamespace) && !NamespaceMover.IsValidNamespace(resultingNamespace))
                throw new McpException($"Invalid destination namespace '{resultingNamespace}'");
            if (preserveBinaryCompatibility && resultingNamespace != oldNamespace)
                throw new McpException($"Type forwarding preserves the original fully-qualified name; {oldNamespace} -> {resultingNamespace} changes that identity");

            var destinationDir = Path.GetDirectoryName(destinationProject.FilePath!)!;
            targetFilePath = targetFilePath is null
                ? Path.Combine(destinationDir, typeName + ".cs")
                : session.ResolvePath(targetFilePath);
            targetFilePath = Path.GetFullPath(targetFilePath);
            var relativeDestination = Path.GetRelativePath(destinationDir, targetFilePath);
            if (Path.IsPathRooted(relativeDestination) || relativeDestination == ".." ||
                relativeDestination.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                !targetFilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                throw new McpException($"Target file {targetFilePath} must be a .cs file under {destinationDir}");
            var destinationPaths = TypeMovePlanner.TargetPaths(targetFilePath, parts.Count);
            foreach (var newPath in destinationPaths)
            {
                if (File.Exists(newPath) || solution.GetDocumentIdsWithFilePath(newPath).Any())
                    throw new McpException($"Destination file already exists: {newPath}");
                if (destinationProject.Documents.Any(d => d.Name == Path.GetFileName(newPath)))
                    throw new McpException($"Destination project already has a document named {Path.GetFileName(newPath)}");
                trace.TargetFilePaths.Add(newPath);
            }

            trace.SourceProjectName = sourceProject.Name;
            trace.TargetProjectName = destinationProject.Name;
            trace.OriginalNamespace = oldNamespace;
            trace.TargetFilePath = targetFilePath;
            trace.Complete($"Selected {type.ToDisplayString()} in {sourceProject.Name}; destination {destinationProject.Name}/{relativeDestination}; {parts.Count} declarations; ABI forwarder={preserveBinaryCompatibility}");

            trace.Begin("find-affected-consumers", "Find symbol references in every project to determine required project dependencies");
            var referencedProjects = new HashSet<ProjectId>();
            var movedSpans = type.DeclaringSyntaxReferences
                .Select(r => (r.SyntaxTree, r.Span)).ToList();
            var references = await SymbolFinder.FindReferencesAsync(type, solution, cancellationToken);
            foreach (var reference in references.SelectMany(r => r.Locations))
            {
                if (movedSpans.Any(m => m.SyntaxTree == reference.Location.SourceTree &&
                                        m.Span.Contains(reference.Location.SourceSpan)))
                    continue; // References within moved parts stay with the type.
                if (reference.Document.Project.Id != destinationProject.Id)
                    referencedProjects.Add(reference.Document.Project.Id);
            }
            var typeDependencies = await TypeMovePlanner.ReferencedProjectIdsAsync(
                solution, type, cancellationToken);
            typeDependencies.Remove(destinationProject.Id);
            var needsOldNamespaceImport = resultingNamespace != oldNamespace &&
                await TypeMovePlanner.NeedsOldNamespaceImportAsync(solution, type, oldNamespace, cancellationToken);
            if (preserveBinaryCompatibility)
                referencedProjects.Add(sourceProject.Id);
            trace.Complete($"Type is consumed by {referencedProjects.Count} projects; " +
                $"moved declarations depend on {typeDependencies.Count} projects");

            var changed = solution;
            if (!string.Equals(resultingNamespace, oldNamespace, StringComparison.Ordinal))
            {
                trace.Begin("rewrite-namespace-and-references",
                    "Rewrite Roslyn-bound references to the new namespace before moving declarations");
                changed = await NamespaceMover.RewriteTypeReferencesOnlyAsync(
                    solution, type, resultingNamespace, cancellationToken);
                trace.Complete($"References rewritten: {oldNamespace} -> {resultingNamespace}");
            }

            trace.Begin("extract-declarations",
                "Move every declaration, including all partial parts, to destination project documents in-memory");
            changed = await TypeMovePlanner.ExtractAsync(changed, parts, typeName,
                oldNamespace, resultingNamespace, destinationProject.Id,
                destinationPaths, needsOldNamespaceImport, cancellationToken);
            trace.Complete($"Prepared {parts.Count} declarations in {destinationProject.Name}; " +
                $"retained unrelated types in source documents");

            if (preserveBinaryCompatibility)
            {
                trace.Begin("add-type-forwarder", "Add a source assembly TypeForwardedTo attribute for binary consumers");
                var forwarderPath = Path.Combine(Path.GetDirectoryName(sourceProject.FilePath!)!,
                    $"RefactorMCP.TypeForwarder.{typeName}.g.cs");
                if (File.Exists(forwarderPath) || changed.GetDocumentIdsWithFilePath(forwarderPath).Any())
                    throw new McpException($"Type forwarding file already exists: {forwarderPath}");
                var fullTypeName = resultingNamespace.Length == 0 ? typeName : $"{resultingNamespace}.{typeName}";
                var forwarderText = $"using System.Runtime.CompilerServices;\n" +
                    $"[assembly: TypeForwardedTo(typeof(global::{fullTypeName}))]\n";
                changed = changed.AddDocument(DocumentId.CreateNewId(sourceProject.Id),
                    Path.GetFileName(forwarderPath), SourceText.From(forwarderText),
                    filePath: forwarderPath);
                trace.AbiForwarderPath = forwarderPath;
                trace.Complete($"Binary compatibility forwarder prepared in {sourceProject.Name}: {fullTypeName}");
            }
            else if (type.DeclaredAccessibility == Accessibility.Public &&
                sourceProject.AssemblyName != destinationProject.AssemblyName)
            {
                trace.ArchitecturalWarnings.Add($"ABI: public type {type.ToDisplayString()} changes " +
                    $"assembly identity from {sourceProject.AssemblyName} to {destinationProject.AssemblyName}; " +
                    $"existing compiled consumers require rebuild unless a type forwarder is enabled");
            }

            trace.Begin("update-project-references",
                "Add project references to destination for consumers and from destination for the moved type's dependencies");
            var edges = referencedProjects.Select(id => (From: id, To: destinationProject.Id))
                .Concat(typeDependencies.Select(id => (From: destinationProject.Id, To: id)))
                .Where(edge => edge.From != edge.To).Distinct().ToList();
            foreach (var (fromId, toId) in edges)
            {
                var from = changed.GetProject(fromId)!;
                var to = changed.GetProject(toId)!;
                if (from.ProjectReferences.Any(r => r.ProjectId == toId))
                    continue;
                var upstream = changed.GetProjectDependencyGraph()
                    .GetProjectsThatThisProjectTransitivelyDependsOn(toId);
                if (upstream.Contains(fromId))
                    throw new McpException($"Adding project reference {from.Name} -> {to.Name} would create " +
                        $"a cycle; {to.Name} already depends on {from.Name}. " +
                        "Extract an interface, reduce the dependency or move the dependent types together.");
                trace.ProjectReferenceChanges.Add($"{from.Name} -> {to.Name}");
                changed = changed.GetProject(fromId)!.AddProjectReference(new ProjectReference(toId)).Solution;
            }
            if (enforceContractsBoundary && TypeMovePlanner.ForbiddenContractsCoreEdge(changed) is { } violation)
                throw new McpException($"Forbidden Contracts -> Core dependency detected: {violation}. " +
                    "Extract the dependent contract or introduce an interface instead.");
            trace.Complete($"{trace.ProjectReferenceChanges.Count} project references added; " +
                "no circular or forbidden Contracts -> Core dependency detected");

            trace.Begin("validate-compilation", "Reject new Roslyn compilation errors in source, destination and dependent projects");
            var diagnostics = await SolutionEdits.NewDiagnosticsAsync(solution, changed,
                diagnostic => diagnostic.Severity == DiagnosticSeverity.Error, cancellationToken);
            trace.Diagnostics.AddRange(diagnostics.Take(100).Select(SolutionEdits.Describe));
            if (diagnostics.Count > 0)
                throw new McpException($"Move introduces {diagnostics.Count} compiler errors. First: {SolutionEdits.Describe(diagnostics[0])}");
            trace.Complete("No new C# compiler errors detected");

            var changedPaths = ChangedPaths(solution, changed).ToList();
            changedPaths.Add(solutionPath);
            foreach (var affected in changed.GetChanges(solution).GetProjectChanges())
            {
                var p = changed.GetProject(affected.ProjectId)?.FilePath ?? solution.GetProject(affected.ProjectId)?.FilePath;
                if (p is not null) changedPaths.Add(p);
            }
            changedPaths = changedPaths.Distinct(PathComparer).ToList();
            trace.FilesPlannedForChange.AddRange(changedPaths);
            if (dryRun)
                return $"DRY RUN PASSED: move {typeName} ({parts.Count} declarations): {sourceProject.Name} -> {destinationProject.Name} " +
                    $"({string.Join(", ", destinationPaths)}), namespace {resultingNamespace}. " +
                    $"Project references: {string.Join(", ", trace.ProjectReferenceChanges.DefaultIfEmpty("(none)"))}. " +
                    $"ABI forwarder: {preserveBinaryCompatibility}. " +
                    $"Warnings: {string.Join("; ", trace.ArchitecturalWarnings.DefaultIfEmpty("(none)"))}. " +
                    $"Affected files: {string.Join(", ", changedPaths)}. No writes performed.";

            trace.Begin("snapshot-filesystem", "Copy original bytes of every affected C# file, project file and solution file for rollback");
            snapshot = new DiskSnapshot(changedPaths);
            trace.Complete($"Captured {changedPaths.Count} file snapshots (including files not yet existing)");

            trace.Begin("check-concurrent-edits", "Reject changed on-disk source content since solution load");
            foreach (var doc in solution.Projects.SelectMany(p => p.Documents)
                         .Where(d => changedPaths.Contains(d.FilePath ?? "", PathComparer)))
            {
                if (doc.FilePath is null || !File.Exists(doc.FilePath)) continue;
                var inWorkspace = (await doc.GetTextAsync(cancellationToken)).ToString();
                var onDisk = await File.ReadAllTextAsync(doc.FilePath, cancellationToken);
                if (!string.Equals(inWorkspace, onDisk, StringComparison.Ordinal))
                    throw new McpException($"Concurrent edit detected in {doc.FilePath}; reload the solution and retry");
            }
            var concurrentEdits = snapshot.ChangedPaths.ToList();
            if (concurrentEdits.Count != 0)
                throw new McpException($"Concurrent edits were detected after snapshot; no writes attempted: {string.Join(", ", concurrentEdits)}");
            trace.Complete("Affected source files and filesystem snapshots match on-disk state");

            trace.Begin("apply-msbuild-workspace", "Persist project references and document edits through MSBuildWorkspace.TryApplyChanges");
            var kinds = new HashSet<ApplyChangesKind> { ApplyChangesKind.ChangeDocument, ApplyChangesKind.AddDocument };
            if (trace.ProjectReferenceChanges.Count > 0)
                kinds.Add(ApplyChangesKind.AddProjectReference);
            // TryApplyChanges may partially update files before returning false/throwing;
            // DiskSnapshot guarantees restoration of original bytes on all such paths.
            applyAttempted = true;
            if (!session.TryApplyChanges(changed, kinds))
                throw new McpException("MSBuildWorkspace.TryApplyChanges returned false");
            trace.FilesWritten.AddRange(changedPaths.Where(path => snapshot.HasChanged(path)));
            trace.Complete($"MSBuild workspace applied changes; {trace.FilesWritten.Count} files changed");

            trace.Begin("normalize-sdk-compile-items", "Avoid duplicate implicit and explicit Compile items in SDK-style projects");
            var normalizedCount = destinationPaths.Count(path =>
                MoveTypePersistence.NormalizeImplicitCompileItem(targetProjectPath, path)) +
                (trace.AbiForwarderPath is { } abiPath &&
                 MoveTypePersistence.NormalizeImplicitCompileItem(sourceProject.FilePath!, abiPath) ? 1 : 0);
            trace.Complete($"Reconciled {normalizedCount} new Compile items with SDK implicit inclusion");

            trace.Begin("verify-persisted-build", "Build the persisted solution and capture MSBuild errors before accepting the move");
            var output = await MoveTypePersistence.BuildAsync(solutionPath, cancellationToken);
            trace.Complete($"dotnet build passed: {output.Trim()}");

            trace.Begin("verify-persisted-result", "Reload persisted solution and verify moved type and project references");
            session.MarkForReload();
            var reloaded = await session.GetOrLoadAsync(cancellationToken: cancellationToken);
            foreach (var destination in destinationPaths)
            {
                if (RefactoringHelpers.GetDocumentByPath(reloaded, destination)?.Project.Name != destinationProject.Name)
                    throw new McpException($"Reloaded solution cannot find {destination} in {destinationProject.Name}");
            }
            if (trace.AbiForwarderPath is { } forwarder &&
                RefactoringHelpers.GetDocumentByPath(reloaded, forwarder)?.Project.Name != sourceProject.Name)
                throw new McpException($"Reloaded solution cannot find ABI forwarder {forwarder} in {sourceProject.Name}");
            foreach (var edge in trace.ProjectReferenceChanges)
            {
                var (fromName, toName) = SplitEdge(edge);
                var from = reloaded.Projects.Single(p => p.Name == fromName);
                var to = reloaded.Projects.Single(p => p.Name == toName);
                if (!from.ProjectReferences.Any(r => r.ProjectId == to.Id))
                    throw new McpException($"Persisted project reference missing: {edge}");
            }
            trace.Complete("Destination document and project references verified after fresh solution reload");
            trace.RollbackStatus = "not needed; committed successfully";
            return $"Successfully moved {typeName} ({parts.Count} declarations) from {sourceProject.Name} to {destinationProject.Name}: " +
                $"{string.Join(", ", destinationPaths)} (namespace {resultingNamespace}); " +
                $"added project references: {trace.ProjectReferenceChanges.Count}; " +
                $"ABI forwarder: {preserveBinaryCompatibility}; " +
                $"warnings: {string.Join("; ", trace.ArchitecturalWarnings.DefaultIfEmpty("(none)"))}; " +
                $"preflight, persisted build and reloaded-solution verification passed.";
        }
        catch (Exception error)
        {
            if (snapshot is not null && applyAttempted)
            {
                trace.FilesWritten.AddRange(snapshot.ChangedPaths.Except(trace.FilesWritten, PathComparer));
                try
                {
                    trace.RollbackStatus = snapshot.Restore();
                }
                catch (Exception rollbackError)
                {
                    trace.RollbackStatus = "FAILED: " + rollbackError;
                }
            }
            session?.MarkForReload();
            try
            {
                var path = trace.SaveFailure(error);
                throw new McpException(trace.FormatFailure(path, error), error);
            }
            catch (McpException) { throw; }
            catch (Exception reportError)
            {
                throw new McpException($"DEFECT_REPORT_REQUIRED: {error}. " +
                    $"JSON reporting also failed: {reportError}. Rollback: {trace.RollbackStatus}. " +
                    "Immediately flag this failure in your ChatGPT thread and submit a DefectReport " +
                    "using semantic plan defect-report create --payload <JSON>.", error);
            }
        }
    }

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static bool PathsEqual(string? left, string right) =>
        left is not null && PathComparer.Equals(Path.GetFullPath(left), Path.GetFullPath(right));

    private static (string From, string To) SplitEdge(string edge)
    {
        var parts = edge.Split(" -> ", 2, StringSplitOptions.None);
        return (parts[0], parts[1]);
    }

    private static IEnumerable<string> ChangedPaths(Solution before, Solution after)
    {
        foreach (var project in after.GetChanges(before).GetProjectChanges())
        {
            foreach (var id in project.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true))
                if (after.GetDocument(id)?.FilePath is { } changed) yield return changed;
            foreach (var id in project.GetAddedDocuments())
                if (after.GetDocument(id)?.FilePath is { } added) yield return added;
            foreach (var id in project.GetRemovedDocuments())
                if (before.GetDocument(id)?.FilePath is { } removed) yield return removed;
        }
    }

    /// <summary>Restores the exact bytes of every file originally present, including project files.</summary>
    private sealed class DiskSnapshot
    {
        private readonly Dictionary<string, byte[]?> _bytes = new(PathComparer);

        public DiskSnapshot(IEnumerable<string> paths)
        {
            foreach (var path in paths)
                _bytes[Path.GetFullPath(path)] = File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public IEnumerable<string> ChangedPaths => _bytes.Keys.Where(HasChanged);

        public bool HasChanged(string path)
        {
            var before = _bytes[Path.GetFullPath(path)];
            var now = File.Exists(path) ? File.ReadAllBytes(path) : null;
            return !(before is null && now is null) && (before is null || now is null || !before.SequenceEqual(now));
        }

        public string Restore()
        {
            var count = 0;
            var failures = new List<string>();
            foreach (var (path, bytes) in _bytes)
            {
                try
                {
                    if (!HasChanged(path)) continue;
                    if (bytes is null)
                    {
                        if (File.Exists(path)) File.Delete(path);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.WriteAllBytes(path, bytes);
                    }
                    count++;
                }
                catch (Exception error) { failures.Add($"{path}: {error.Message}"); }
            }
            return failures.Count == 0
                ? $"succeeded: restored {count} modified/deleted/created files from byte-exact snapshots"
                : $"INCOMPLETE: restored {count} files; failures: {string.Join("; ", failures)}";
        }
    }
}
