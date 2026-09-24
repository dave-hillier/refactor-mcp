using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.Infrastructure;

public class StateDirectoryTests : IDisposable
{
    private readonly string? _originalHome = Environment.GetEnvironmentVariable("REFACTOR_MCP_HOME");
    private readonly string? _originalLog = Environment.GetEnvironmentVariable("REFACTOR_MCP_LOG");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("REFACTOR_MCP_HOME", _originalHome);
        Environment.SetEnvironmentVariable("REFACTOR_MCP_LOG", _originalLog);
        UnloadSolutionTool.ClearSolutionCache();
    }

    [Fact]
    public void Root_DefaultsToTheHomeDirectory()
    {
        Environment.SetEnvironmentVariable("REFACTOR_MCP_HOME", null);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(Path.Combine(home, ".refactor-mcp"), StateDirectory.Root);
    }

    [Fact]
    public void For_IsOutsideTheSolutionDirectory()
    {
        Environment.SetEnvironmentVariable("REFACTOR_MCP_HOME", null);
        var solutionPath = TestUtilities.GetSolutionPath();
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;

        var state = StateDirectory.For(solutionPath);

        Assert.False(state.StartsWith(solutionDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        Assert.StartsWith(StateDirectory.Root, state);
    }

    [Fact]
    public void For_KeepsTwoCheckoutsOfTheSameSolutionApart()
    {
        var first = StateDirectory.For(Path.Combine(Path.GetTempPath(), "a", "Shop.sln"));
        var second = StateDirectory.For(Path.Combine(Path.GetTempPath(), "b", "Shop.sln"));

        Assert.NotEqual(first, second);
        Assert.StartsWith("Shop-", Path.GetFileName(first));
    }

    [Fact]
    public void Root_HonoursTheEnvironmentVariable()
    {
        var custom = Path.Combine(Path.GetTempPath(), "refactor-mcp-state");
        Environment.SetEnvironmentVariable("REFACTOR_MCP_HOME", custom);

        Assert.Equal(custom, StateDirectory.Root);
        Assert.StartsWith(custom, StateDirectory.Metrics(TestUtilities.GetSolutionPath()));
    }

    [Fact]
    public async Task LoadSolution_WritesNothingBesideTheSolution()
    {
        var custom = Path.Combine(Path.GetTempPath(), "refactor-mcp-state-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("REFACTOR_MCP_HOME", custom);
        var fixture = Path.Combine(Path.GetTempPath(), "refactor-mcp-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        var solutionPath = Path.Combine(fixture, "Empty.sln");
        File.WriteAllText(solutionPath, "\nMicrosoft Visual Studio Solution File, Format Version 12.00\n");

        try
        {
            await LoadSolutionTool.LoadSolution(solutionPath);

            Assert.False(Directory.Exists(Path.Combine(fixture, ".refactor-mcp")));
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
            if (Directory.Exists(custom))
                Directory.Delete(custom, recursive: true);
        }
    }

    [Fact]
    public void ToolCallLog_GoesInTheSolutionsStateDirectory()
    {
        Environment.SetEnvironmentVariable("REFACTOR_MCP_HOME", Path.Combine(Path.GetTempPath(), "refactor-mcp-state"));
        Environment.SetEnvironmentVariable("REFACTOR_MCP_LOG", "1");
        var solutionPath = TestUtilities.GetSolutionPath();
        SessionRegistry.GetOrCreate(solutionPath);

        var log = ToolCallLogger.ResolveLogFile();

        Assert.NotNull(log);
        Assert.Equal(StateDirectory.For(solutionPath), Path.GetDirectoryName(log));
    }
}
