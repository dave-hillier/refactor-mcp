using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.Infrastructure;

public class SessionRegistryTests : IDisposable
{
    private static readonly string SolutionPath = TestUtilities.GetSolutionPath();

    public SessionRegistryTests()
    {
        // Sessions are process wide, so start from a known state.
        UnloadSolutionTool.ClearSolutionCache();
    }

    public void Dispose()
    {
        UnloadSolutionTool.ClearSolutionCache();
    }

    [Fact]
    public void GetOrCreate_ReturnsTheSameSession()
    {
        var first = SessionRegistry.GetOrCreate(SolutionPath);
        var second = SessionRegistry.GetOrCreate(SolutionPath);

        Assert.Same(first, second);
        Assert.Equal(Path.GetFullPath(SolutionPath), first.SolutionPath);
    }

    [Fact]
    public void GetOrCreate_IsKeyedByAbsolutePath()
    {
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), SolutionPath);

        Assert.Same(SessionRegistry.GetOrCreate(SolutionPath), SessionRegistry.GetOrCreate(relative));
    }

    [Fact]
    public void GetOrCreate_KeepsSolutionsApart()
    {
        var first = SessionRegistry.GetOrCreate(SolutionPath);
        var second = SessionRegistry.GetOrCreate(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "Other.sln"));

        Assert.NotSame(first, second);
        Assert.Same(first, SessionRegistry.Find(SolutionPath));
        Assert.Same(second, SessionRegistry.Find(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "Other.sln")));
    }

    [Fact]
    public void Find_UnknownPath_ReturnsNull()
    {
        Assert.Null(SessionRegistry.Find(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "Missing.sln")));
        Assert.Null(SessionRegistry.Find(""));
    }

    [Fact]
    public async Task GetOrLoadSolution_LoadsOnceAndReusesTheSolution()
    {
        var first = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var session = SessionRegistry.Find(SolutionPath);

        Assert.NotNull(session);
        Assert.True(session!.IsLoaded);

        var second = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task Unload_ForgetsTheSolutionSoTheNextCallReloads()
    {
        var first = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);

        Assert.True(SessionRegistry.Unload(SolutionPath));
        Assert.Null(SessionRegistry.Find(SolutionPath));

        var second = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task ClearAllCaches_DropsEverySession()
    {
        await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        Assert.NotEmpty(SessionRegistry.All);

        RefactoringHelpers.ClearAllCaches();

        Assert.Empty(SessionRegistry.All);
        Assert.Null(SessionRegistry.Current);
    }

    [Fact]
    public async Task LoadSolution_StartsANewSession()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var first = SessionRegistry.Find(SolutionPath)!.Solution;

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var second = SessionRegistry.Find(SolutionPath)!.Solution;

        Assert.NotNull(first);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task UnloadSolution_ToolReportsWhetherASolutionWasLoaded()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        Assert.Contains("Unloaded solution", UnloadSolutionTool.UnloadSolution(SolutionPath));
        Assert.Contains("was not loaded", UnloadSolutionTool.UnloadSolution(SolutionPath));
    }

    [Fact]
    public async Task MarkForReload_LoadsTheSolutionAgainOnTheNextCall()
    {
        var session = SessionRegistry.GetOrCreate(SolutionPath);
        var first = await session.GetOrLoadAsync();
        var loadedAt = session.LoadedAtUtc;

        session.MarkForReload();
        var second = await session.GetOrLoadAsync();

        Assert.NotSame(first, second);
        Assert.NotEqual(loadedAt, session.LoadedAtUtc);
    }

    [Fact]
    public void MarkForReload_OnAnUnloadedSession_IsHarmless()
    {
        var session = SessionRegistry.GetOrCreate(SolutionPath);

        session.MarkForReload();

        Assert.False(session.IsLoaded);
    }

    [Fact]
    public async Task HandleFileChange_IgnoresFilesTheSolutionDoesNotContain()
    {
        var session = SessionRegistry.GetOrCreate(SolutionPath);
        var solution = await session.GetOrLoadAsync();
        var unrelated = Path.Combine(session.SolutionDirectory, "notes.md");

        session.HandleFileChange(unrelated, WatcherChangeTypes.Changed);
        session.HandleFileChange(Path.Combine(session.SolutionDirectory, "not-in-solution.cs"), WatcherChangeTypes.Changed);

        Assert.Same(solution, session.Solution);
    }

    [Fact]
    public async Task HandleFileChange_ForAProjectFile_AsksForAReload()
    {
        var session = SessionRegistry.GetOrCreate(SolutionPath);
        var solution = await session.GetOrLoadAsync();

        session.HandleFileChange(Path.Combine(session.SolutionDirectory, "RefactorMCP.sln"), WatcherChangeTypes.Changed);

        // The reload happens on the next call, which is a different solution.
        var reloaded = await session.GetOrLoadAsync();
        Assert.NotSame(solution, reloaded);
    }

    [Fact]
    public void ResolvePath_UsesTheSolutionDirectoryForRelativePaths()
    {
        var session = new SolutionSession(Path.Combine(Path.GetTempPath(), "refactor-mcp-tests", "App.sln"));
        var expected = Path.Combine(Path.GetTempPath(), "refactor-mcp-tests", "src", "Foo.cs");

        Assert.Equal(Path.GetFullPath(expected), session.ResolvePath(Path.Combine("src", "Foo.cs")));
        Assert.Equal(Path.GetFullPath(expected), session.ResolvePath(expected));
        Assert.Equal(string.Empty, session.ResolvePath(string.Empty));
    }

    [Fact]
    public void ResolvePath_IsRelativeToEachSession()
    {
        var root = Path.Combine(Path.GetTempPath(), "refactor-mcp-tests");
        var first = new SolutionSession(Path.Combine(root, "one", "One.sln"));
        var second = new SolutionSession(Path.Combine(root, "two", "Two.sln"));

        Assert.Equal(Path.GetFullPath(Path.Combine(root, "one", "Foo.cs")), first.ResolvePath("Foo.cs"));
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "two", "Foo.cs")), second.ResolvePath("Foo.cs"));
    }

    [Fact]
    public void MoveHistory_BelongsToTheSession()
    {
        var first = SessionRegistry.GetOrCreate(SolutionPath);
        var second = SessionRegistry.GetOrCreate(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "Other.sln"));

        first.MarkMoved(SolutionPath, "Calculate");

        Assert.Throws<ModelContextProtocol.McpException>(() => first.EnsureNotAlreadyMoved(SolutionPath, "Calculate"));
        second.EnsureNotAlreadyMoved(SolutionPath, "Calculate");

        first.ResetMoveHistory();
        first.EnsureNotAlreadyMoved(SolutionPath, "Calculate");
    }

    [Fact]
    public void MoveHistory_ResolvesRelativeFilePathsAgainstTheSolution()
    {
        var session = new SolutionSession(SolutionPath);
        var relative = Path.GetRelativePath(session.SolutionDirectory, SolutionPath);

        session.MarkMoved(relative, "Calculate");

        Assert.Throws<ModelContextProtocol.McpException>(() => session.EnsureNotAlreadyMoved(SolutionPath, "Calculate"));
    }
}

public class DaemonEndpointTests
{
    [Fact]
    public void AnEndpointIsDerivedFromTheSolutionPath()
    {
        var solution = Path.Combine(Path.GetTempPath(), "refactor-mcp-tests", "App.sln");
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), solution);

        Assert.Equal(DaemonEndpoint.For(solution).SocketPath, DaemonEndpoint.For(relative).SocketPath);
        Assert.Equal(Path.GetFullPath(solution), DaemonEndpoint.For(solution).SolutionPath);
    }

    [Fact]
    public void DifferentSolutionsDoNotShareASocket()
    {
        var directory = Path.Combine(Path.GetTempPath(), "refactor-mcp-tests");

        Assert.NotEqual(
            DaemonEndpoint.For(Path.Combine(directory, "One.sln")).SocketPath,
            DaemonEndpoint.For(Path.Combine(directory, "Two.sln")).SocketPath);
    }

    [Fact]
    public void SocketsStayInTheDiscoverableDirectoryWithAShortName()
    {
        // Unix domain socket paths are limited to about a hundred bytes, so the
        // directory is all the room there is for context.
        var endpoint = DaemonEndpoint.For(Path.Combine(Path.GetTempPath(), "refactor-mcp-tests", "A.sln"));

        Assert.Equal(DaemonEndpoint.Directory, Path.GetDirectoryName(endpoint.SocketPath));
        Assert.True(Path.GetFileName(endpoint.SocketPath).Length <= 32, endpoint.SocketPath);
    }
}
