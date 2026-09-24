using System.IO;
using Xunit;

namespace RefactorMCP.Tests.Infrastructure;

public class SolutionFileChangesTests
{
    [Theory]
    [InlineData("src/App.cs", FileChangeAction.UpdateDocument)]
    [InlineData("src/App.CS", FileChangeAction.UpdateDocument)]
    [InlineData("src/App.cs", FileChangeAction.Reload, WatcherChangeTypes.Created)]
    [InlineData("src/App.cs", FileChangeAction.Reload, WatcherChangeTypes.Deleted)]
    [InlineData("src/App.csproj", FileChangeAction.Reload)]
    [InlineData("App.sln", FileChangeAction.Reload)]
    [InlineData("App.slnx", FileChangeAction.Reload)]
    [InlineData("Directory.Build.props", FileChangeAction.Reload)]
    [InlineData("Directory.Packages.props", FileChangeAction.Reload)]
    [InlineData("global.json", FileChangeAction.Reload)]
    [InlineData("src/App.md", FileChangeAction.Ignore)]
    [InlineData("src/data.json", FileChangeAction.Ignore)]
    [InlineData("bin/App.dll", FileChangeAction.Ignore)]
    public void ChangesAreClassified(string path, FileChangeAction expected, WatcherChangeTypes change = WatcherChangeTypes.Changed)
    {
        Assert.Equal(expected, SolutionFileChanges.Classify(path, change));
    }
}
