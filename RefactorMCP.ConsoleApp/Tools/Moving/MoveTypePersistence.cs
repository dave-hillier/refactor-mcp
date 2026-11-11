using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

/// <summary>Reconcile SDK implicit item semantics and verify the persisted solution builds.</summary>
internal static class MoveTypePersistence
{
    /// <summary>
    /// MSBuildWorkspace writes an explicit Compile Include for new documents.
    /// In SDK projects that already include **/*.cs implicitly, that creates NETSDK1022.
    /// Convert only that new Include to Update if it contains metadata; remove it if empty.
    /// </summary>
    public static bool NormalizeImplicitCompileItem(string projectPath, string newFilePath)
    {
        var xml = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
        var project = xml.Root ?? throw new InvalidOperationException("The destination project has no XML root");
        if (project.Name.LocalName != "Project")
            throw new InvalidOperationException($"Unexpected project XML root: {project.Name}");
        if (project.Attribute("Sdk") is null &&
            !project.Elements().Any(e => e.Name.LocalName == "Sdk"))
            return false;

        // Explicit disabling can appear in the .csproj; for imported configuration the
        // post-write build verifies what MSBuild actually evaluates.
        if (project.Descendants().Any(e => e.Name.LocalName == "EnableDefaultCompileItems" &&
                                             e.Value.Trim().Equals("false", StringComparison.OrdinalIgnoreCase)))
            return false;

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
        var changed = false;
        foreach (var item in project.Descendants()
                     .Where(e => e.Name.LocalName == "Compile" && e.Attribute("Include") is not null).ToArray())
        {
            var include = item.Attribute("Include")!.Value;
            if (include.Contains('*') || include.Contains('$') || Path.IsPathRooted(include))
                continue;
            var candidate = Path.GetFullPath(Path.Combine(projectDirectory,
                include.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
            if (!PathsEqual(candidate, newFilePath))
                continue;
            if (item.HasElements || !string.IsNullOrWhiteSpace(item.Value) || item.HasAttributes &&
                item.Attributes().Any(a => a.Name.LocalName != "Include"))
            {
                // Metadata from workspace must remain attached without double-compiling the item.
                item.SetAttributeValue("Update", include);
                item.Attribute("Include")!.Remove();
            }
            else
            {
                item.Remove();
            }
            changed = true;
        }
        if (changed)
            xml.Save(projectPath, SaveOptions.DisableFormatting);
        return changed;
    }

    public static async Task<string> BuildAsync(string solutionPath, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(solutionPath)!
        };
        process.StartInfo.ArgumentList.Add("build");
        process.StartInfo.ArgumentList.Add(solutionPath);
        process.StartInfo.ArgumentList.Add("--verbosity");
        process.StartInfo.ArgumentList.Add("quiet");
        // MSBuildLocator binds the workspace to one installed SDK in this process.
        // That private binding must not leak into a fresh dotnet build child, which
        // should select its SDK normally (global.json / installed SDKs).
        foreach (var key in new[] { "MSBUILD_EXE_PATH", "MSBuildSDKsPath", "MSBuildExtensionsPath",
                     "MSBuildToolsPath", "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR",
                     "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER", "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR" })
            process.StartInfo.Environment.Remove(key);
        process.Start();
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = (await stdout) + "\n" + (await stderr);
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"dotnet build failed (exit {process.ExitCode}): {output[^Math.Min(output.Length, 10000)..]}");
            return output;
        }
        catch
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
