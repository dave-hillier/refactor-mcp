using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class ExtractInterfaceTests : TestBase
{
    [Fact]
    public async Task ExtractInterface_CreatesInterfaceFile()
    {
        UnloadSolutionTool.ClearSolutionCache();
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "ExtractInterface.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForExtractInterface());
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var interfacePath = Path.Combine(Path.GetDirectoryName(testFile)!, "IPerson.cs");
        var result = await ExtractInterfaceTool.ExtractInterface(
            SolutionPath,
            testFile,
            "Person",
            "Name,Greet",
            interfacePath);

        Assert.Contains("Successfully extracted interface", result);
        Assert.True(File.Exists(interfacePath));
        var iface = await File.ReadAllTextAsync(interfacePath);
        Assert.Contains("interface IPerson", iface);
        Assert.Contains("string Name", iface);
        Assert.Contains("void Greet()", iface);
        var source = await File.ReadAllTextAsync(testFile);
        Assert.Contains("class Person : IPerson", source);
    }
}
