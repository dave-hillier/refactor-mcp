using System;
using System.IO;

namespace RefactorMCP.Tests;

public abstract class TestBase : IDisposable
{
    protected static readonly string SolutionPath = TestUtilities.GetSolutionPath();
    protected static readonly string ExampleFilePath = Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs");
    protected static readonly string TestOutputPath =
        Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "TestOutput");

    protected TestBase()
    {
        Directory.CreateDirectory(TestOutputPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(TestOutputPath))
            Directory.Delete(TestOutputPath, true);
    }
}
