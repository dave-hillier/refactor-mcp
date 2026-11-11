using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using RefactorMCP.ConsoleApp.Tools.Moving;
using Xunit;

namespace RefactorMCP.Tests.Tools;

[CollectionDefinition("MoveTypeToProjectSerial", DisableParallelization = true)]
public sealed class MoveTypeToProjectSerialCollection { }

[Collection("MoveTypeToProjectSerial")]
public sealed class MoveTypeToProjectToolTests
{
    private const string Consumer = """
        using Demo.Core;
        namespace Demo.Consumer;
        public sealed class Client { public WorkContract Contract { get; } = new(); }
        """;

    [Fact]
    public async Task MovePartialType_MovesEveryPart_UpdatesNamespaceAndCompiles()
    {
        using var fixture = await TestFixture.CreateAsync(
            """
            namespace Demo.Core;
            public partial class WorkContract { public int A => 1; }
            public sealed class StayA { }
            """,
            """
            namespace Demo.Core;
            public partial class WorkContract { public int B => 2; }
            public sealed class StayB { }
            """,
            Consumer);

        var result = await MoveTypeToProjectTool.MoveTypeToProject(
            fixture.SolutionPath, fixture.SourceFilePath, "WorkContract",
            fixture.DestinationProjectPath, targetNamespace: "Demo.Contracts",
            purpose: "Qualification: extraction into Contracts");

        Assert.Contains("2 declarations", result);
        Assert.True(File.Exists(Path.Combine(fixture.DestinationDirectory, "WorkContract.cs")));
        Assert.True(File.Exists(Path.Combine(fixture.DestinationDirectory, "WorkContract.Part02.cs")));
        Assert.Contains("class StayA", await File.ReadAllTextAsync(fixture.SourceFilePath));
        Assert.Contains("class StayB", await File.ReadAllTextAsync(fixture.SecondSourceFilePath));
        Assert.Contains("namespace Demo.Contracts", await File.ReadAllTextAsync(
            Path.Combine(fixture.DestinationDirectory, "WorkContract.Part02.cs")));
        Assert.Contains("ProjectReference", await File.ReadAllTextAsync(fixture.ConsumerProjectPath));
        await fixture.BuildAsync();
    }

    [Fact]
    public async Task DryRun_ReportsReferences_AndChangesNoFiles()
    {
        using var fixture = await TestFixture.CreateAsync(
            "namespace Demo.Core; public class WorkContract { }",
            null, Consumer);
        var beforeSource = await File.ReadAllBytesAsync(fixture.SourceFilePath);
        var beforeConsumer = await File.ReadAllBytesAsync(fixture.ConsumerProjectPath);

        var result = await MoveTypeToProjectTool.MoveTypeToProject(
            fixture.SolutionPath, fixture.SourceFilePath, "WorkContract",
            fixture.DestinationProjectPath, dryRun: true);

        Assert.StartsWith("DRY RUN PASSED", result);
        Assert.Equal(beforeSource, await File.ReadAllBytesAsync(fixture.SourceFilePath));
        Assert.Equal(beforeConsumer, await File.ReadAllBytesAsync(fixture.ConsumerProjectPath));
        Assert.False(File.Exists(Path.Combine(fixture.DestinationDirectory, "WorkContract.cs")));
    }

    [Fact]
    public async Task BinaryCompatibility_GeneratesForwarder_AndBuilds()
    {
        using var fixture = await TestFixture.CreateAsync(
            "namespace Demo.Core; public class WorkContract { }",
            null, Consumer);

        var result = await MoveTypeToProjectTool.MoveTypeToProject(
            fixture.SolutionPath, fixture.SourceFilePath, "WorkContract",
            fixture.DestinationProjectPath, preserveBinaryCompatibility: true);

        Assert.Contains("ABI forwarder: True", result);
        var forwarder = await File.ReadAllTextAsync(Path.Combine(
            fixture.SourceDirectory, "RefactorMCP.TypeForwarder.WorkContract.g.cs"));
        Assert.Contains("TypeForwardedTo(typeof(global::Demo.Core.WorkContract))", forwarder);
        await fixture.BuildAsync();
    }

    [Fact]
    public async Task ContractsDependingOnCore_RefusesMoveAndRetainsSource()
    {
        using var fixture = await TestFixture.CreateAsync(
            """
            namespace Demo.Core;
            public class CoreHelper { }
            public class WorkContract { public CoreHelper Dependency { get; } = new(); }
            """, null, Consumer);
        var sourceBefore = await File.ReadAllBytesAsync(fixture.SourceFilePath);
        var targetProjectBefore = await File.ReadAllBytesAsync(fixture.DestinationProjectPath);

        var error = await Assert.ThrowsAsync<McpException>(() =>
            MoveTypeToProjectTool.MoveTypeToProject(
                fixture.SolutionPath, fixture.SourceFilePath, "WorkContract",
                fixture.DestinationProjectPath));

        Assert.Contains("DEFECT_REPORT_REQUIRED", error.Message);
        var path = Regex.Match(error.Message, @"Reproduction JSON: ([^\r\n]+)").Groups[1].Value;
        Assert.True(File.Exists(path), error.Message);
        using var report = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal("update-project-references", report.RootElement.GetProperty("failedStage").GetString());
        Assert.Contains("Forbidden Contracts -> Core",
            report.RootElement.GetProperty("exception").GetProperty("message").GetString());
        Assert.Equal(sourceBefore, await File.ReadAllBytesAsync(fixture.SourceFilePath));
        Assert.Equal(targetProjectBefore, await File.ReadAllBytesAsync(fixture.DestinationProjectPath));
        Assert.False(File.Exists(Path.Combine(fixture.DestinationDirectory, "WorkContract.cs")));
    }

    private sealed class TestFixture : IDisposable
    {
        public string DirectoryPath { get; }
        public string SourceDirectory => Path.Combine(DirectoryPath, "Core");
        public string DestinationDirectory => Path.Combine(DirectoryPath, "Contracts");
        public string SourceFilePath => Path.Combine(SourceDirectory, "A.cs");
        public string SecondSourceFilePath => Path.Combine(SourceDirectory, "B.cs");
        public string SolutionPath => Path.Combine(DirectoryPath, "Move.slnx");
        public string DestinationProjectPath => Path.Combine(DestinationDirectory, "Contracts.csproj");
        public string ConsumerProjectPath => Path.Combine(DirectoryPath, "Consumer", "Consumer.csproj");

        private TestFixture(string directory) => DirectoryPath = directory;

        public static async Task<TestFixture> CreateAsync(string firstPart, string? secondPart, string consumer)
        {
            var root = Path.Combine(Path.GetTempPath(), "RefactorMCP.MoveTypeToProject.Tests",
                Guid.NewGuid().ToString("N"));
            var fixture = new TestFixture(root);
            Directory.CreateDirectory(fixture.SourceDirectory);
            Directory.CreateDirectory(fixture.DestinationDirectory);
            Directory.CreateDirectory(Path.Combine(root, "Consumer"));

            await File.WriteAllTextAsync(fixture.SolutionPath, """
                <Solution>
                  <Project Path="Core/Core.csproj" />
                  <Project Path="Contracts/Contracts.csproj" />
                  <Project Path="Consumer/Consumer.csproj" />
                </Solution>
                """);
            var csproj = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                </Project>
                """;
            await File.WriteAllTextAsync(Path.Combine(fixture.SourceDirectory, "Core.csproj"), csproj);
            await File.WriteAllTextAsync(fixture.DestinationProjectPath, csproj);
            await File.WriteAllTextAsync(fixture.ConsumerProjectPath,
                csproj.Replace("</Project>",
                    """<ItemGroup><ProjectReference Include="../Core/Core.csproj" /></ItemGroup></Project>"""));
            await File.WriteAllTextAsync(fixture.SourceFilePath, firstPart);
            if (secondPart is not null)
                await File.WriteAllTextAsync(fixture.SecondSourceFilePath, secondPart);
            await File.WriteAllTextAsync(Path.Combine(root, "Consumer", "Client.cs"), consumer);
            await fixture.BuildAsync();
            return fixture;
        }

        public async Task BuildAsync()
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = DirectoryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.StartInfo.ArgumentList.Add("build");
            process.StartInfo.ArgumentList.Add(SolutionPath);
            process.StartInfo.ArgumentList.Add("--verbosity");
            process.StartInfo.ArgumentList.Add("quiet");
            foreach (var name in new[] { "MSBUILD_EXE_PATH", "MSBuildSDKsPath", "MSBuildExtensionsPath",
                "MSBuildToolsPath", "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR",
                "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER", "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR" })
                process.StartInfo.Environment.Remove(name);
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.True(process.ExitCode == 0,
                $"Failed to build fixture at {DirectoryPath}: {await output}\n{await error}");
        }

        public void Dispose()
        {
            UnloadSolutionTool.UnloadSolution(SolutionPath);
            try { Directory.Delete(DirectoryPath, recursive: true); }
            catch (IOException) { /* Preserve locked fixture for debugging. */ }
        }
    }
}
