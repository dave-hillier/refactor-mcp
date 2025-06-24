using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class UseInterfaceToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task UseInterface_ChangesParameterType()
    {
        const string initialCode = """
public class Service
{
    public void Process(Logger logger) { }
}

public class Logger { }
public interface ILogger { }
""";

        const string expectedCode = """
public class Service
{
    public void Process(ILogger logger) { }
}

public class Logger { }
public interface ILogger { }
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "UseInterface.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await UseInterfaceTool.UseInterface(
            SolutionPath,
            testFile,
            "Process",
            "logger",
            "ILogger");

        Assert.Contains("Successfully changed parameter", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }
}
