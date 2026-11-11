using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

/// <summary>Durable, self-contained failure evidence and the actionable Recorder intake instruction.</summary>
internal sealed class TypeMoveTrace
{
    private readonly List<TraceStep> _steps = new();
    private string _stage = "initialize";

    public string SolutionPath { get; }
    public string SourcePath { get; }
    public string TypeName { get; }
    public string TargetProjectPath { get; }
    public string? TargetNamespace { get; }
    public string? TargetFilePath { get; set; }
    public List<string> TargetFilePaths { get; } = new();
    public List<string> ArchitecturalWarnings { get; } = new();
    public string? AbiForwarderPath { get; set; }
    public string? Purpose { get; }
    public string? SourceProjectName { get; set; }
    public string? TargetProjectName { get; set; }
    public string? OriginalNamespace { get; set; }
    public string RollbackStatus { get; set; } = "no filesystem changes made";
    public List<string> ProjectReferenceChanges { get; } = new();
    public List<string> FilesPlannedForChange { get; } = new();
    public List<string> FilesWritten { get; } = new();
    public List<string> Diagnostics { get; } = new();
    public string CurrentStage => _stage;

    public TypeMoveTrace(string solutionPath, string sourcePath, string typeName,
        string targetProjectPath, string? targetNamespace, string? purpose)
    {
        SolutionPath = solutionPath;
        SourcePath = sourcePath;
        TypeName = typeName;
        TargetProjectPath = targetProjectPath;
        TargetNamespace = targetNamespace;
        Purpose = purpose;
    }

    public void Begin(string stage, string detail)
    {
        _stage = stage;
        _steps.Add(new TraceStep(stage, detail, "started", DateTimeOffset.UtcNow));
    }

    public void Complete(string detail)
    {
        _steps.Add(new TraceStep(_stage, detail, "succeeded", DateTimeOffset.UtcNow));
    }

    public string SaveFailure(Exception error)
    {
        var when = DateTimeOffset.UtcNow;
        var home = Environment.GetEnvironmentVariable("REFACTOR_MCP_HOME");
        var directory = Path.Combine(
            string.IsNullOrWhiteSpace(home)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".refactor-mcp")
                : Path.GetFullPath(home), "defect-reports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"move-type-to-project-{when:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.json");
        var commit = GitHead(SourcePath);
        var toolVersion = typeof(MoveTypeToProjectTool).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "unknown";
        var toolCommit = GitHead(AppContext.BaseDirectory);
        if (toolCommit == "unavailable" && toolVersion.Contains('+'))
            toolCommit = toolVersion[(toolVersion.IndexOf('+') + 1)..];
        var summary = $"refactor-mcp move-type-to-project failed at {_stage}: {TypeName}";
        var description = $"Operation: move {TypeName} from {SourcePath} to {TargetProjectPath}" +
            $" (destination {TargetFilePath ?? "(not resolved)"}; namespace {TargetNamespace ?? "(preserve)"})" +
            $". Aim: {Purpose ?? "extract type safely across projects"}." +
            $" Git commit: {commit}. Stage: {_stage}. Error: {error.Message}." +
            $" Rollback: {RollbackStatus}. Full reproduction trace: {path}." +
            $" Completed stages: {string.Join("; ", _steps.Where(s => s.Status == "succeeded").Select(s => s.Stage))}.";

        // These fields match the canonical PLAN DefectReport.Create payload.
        var recorderPayload = new
        {
            project = "COD",
            title = summary,
            summary,
            description,
            targetProject = "COD",
            definitionRef = "PLAN:definition-0014",
            defectType = "runtime",
            semanticCharacteristics = new[] { "refactor-mcp", "roslyn", "cross-project-type-move", "automatic-rollback" },
            affectedRecordRefs = Array.Empty<string>()
        };
        var report = new
        {
            schemaVersion = "refactor-mcp/move-type-to-project-defect/v1",
            reportId = Path.GetFileNameWithoutExtension(path),
            recordedAtUtc = when,
            tool = "move-type-to-project",
            action = "move top-level C# type across projects, repair references/dependencies, persist",
            aim = Purpose,
            source = new { solutionPath = SolutionPath, filePath = SourcePath, projectName = SourceProjectName,
                typeName = TypeName, namespaceName = OriginalNamespace },
            destination = new { projectPath = TargetProjectPath, projectName = TargetProjectName,
                filePath = TargetFilePath, filePaths = TargetFilePaths, namespaceName = TargetNamespace ?? OriginalNamespace },
            architectureWarnings = ArchitecturalWarnings,
            abiForwarderPath = AbiForwarderPath,
            environment = new { machine = Environment.MachineName, os = RuntimeInformation.OSDescription,
                dotnet = RuntimeInformation.FrameworkDescription, process = Environment.ProcessPath,
                workingDirectory = Environment.CurrentDirectory, sourceGitCommit = commit,
                toolGitCommit = toolCommit, toolVersion,
                sourceGitWorktreeStatus = GitCommand(SourcePath, "status", "--porcelain", "--untracked-files=no"),
                toolGitWorktreeStatus = GitCommand(AppContext.BaseDirectory, "status", "--porcelain", "--untracked-files=no") },
            failedStage = _stage,
            steps = _steps,
            successfulSteps = _steps.Where(s => s.Status == "succeeded").Select(s => s.Stage).ToArray(),
            projectReferenceChanges = ProjectReferenceChanges,
            filesPlannedForChange = FilesPlannedForChange,
            filesWritten = FilesWritten,
            diagnostics = Diagnostics,
            rollback = RollbackStatus,
            exception = new { type = error.GetType().FullName, message = error.Message, stackTrace = error.ToString() },
            defectReportPayload = recorderPayload,
            submission = new
            {
                command = "semantic plan defect-report create --payload <JSON>",
                powershell = "$r = Get-Content -LiteralPath '" + path.Replace("'", "''") +
                    "' -Raw | ConvertFrom-Json; $payload = $r.defectReportPayload | ConvertTo-Json -Depth 25 -Compress; semantic plan defect-report create --payload $payload",
                verification = "ttd show <returned-defect-report-ref>",
                escalation = "IMMEDIATELY report this defect in the current ChatGPT thread, including report ID, file path, failed step, error, rollback status, Recorder submission result and returned canonical reference."
            }
        };
        File.WriteAllText(path, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    public string FormatFailure(string path, Exception error) => $"""
        DEFECT_REPORT_REQUIRED: move-type-to-project failed at stage '{_stage}'.
        Error: {error.Message[..Math.Min(error.Message.Length, 600)]}
        Reproduction JSON: {path}
        Rollback: {RollbackStatus}
        ACTION FOR OPERATING CHATGPT THREAD: Immediately post this failure, its cause, rollback status, and full report path in your thread output. Submit the canonical DefectReport now:
          $r = Get-Content -LiteralPath '{path.Replace("'", "''")}' -Raw | ConvertFrom-Json
          $payload = $r.defectReportPayload | ConvertTo-Json -Depth 25 -Compress
          semantic plan defect-report create --payload $payload
        Verify with: ttd show <returned-defect-report-ref>
        Report the returned canonical reference or submission error visibly in the same thread. Preserve JSON and stack trace for debugging.
        """;

    private static string GitHead(string source) =>
        GitCommand(source, "rev-parse", "HEAD");

    private static string GitCommand(string source, params string[] args)
    {
        try
        {
            var dir = Directory.Exists(source) ? source : Path.GetDirectoryName(Path.GetFullPath(source))!;
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.StartInfo.ArgumentList.Add("-C");
            process.StartInfo.ArgumentList.Add(dir);
            foreach (var arg in args)
                process.StartInfo.ArgumentList.Add(arg);
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return process.ExitCode == 0 ? output.Trim() : "unavailable";
        }
        catch { return "unavailable"; }
    }

    private sealed record TraceStep(string Stage, string Detail, string Status, DateTimeOffset AtUtc);
}
