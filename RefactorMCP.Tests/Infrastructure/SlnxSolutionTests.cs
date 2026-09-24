using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.Infrastructure;

public class SlnxSolutionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SlnxTest_" + Guid.NewGuid().ToString("N"));

    public SlnxSolutionTests()
    {
        UnloadSolutionTool.ClearSolutionCache();
        Directory.CreateDirectory(Path.Combine(_directory, "Library"));
        File.WriteAllText(Path.Combine(_directory, "Library", "Library.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(_directory, "Library", "Greeter.cs"), "public class Greeter { }");
        File.WriteAllText(SolutionPath, """
            <Solution>
              <Project Path="Library/Library.csproj" />
            </Solution>
            """);
    }

    private string SolutionPath => Path.Combine(_directory, "App.slnx");

    public void Dispose()
    {
        UnloadSolutionTool.ClearSolutionCache();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Fact]
    public async Task LoadSolution_OpensAnSlnxFile()
    {
        var message = await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        Assert.Contains("App.slnx", message);
        var solution = SessionRegistry.Find(SolutionPath)!.Solution!;
        var project = Assert.Single(solution.Projects);
        Assert.Equal("Library", project.Name);
        Assert.Contains(project.Documents, d => d.Name == "Greeter.cs");
    }
}
