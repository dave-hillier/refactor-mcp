using System;
using System.IO;

namespace RefactorMCP.Tests;

public abstract class TestBase : IDisposable
{
    protected static readonly string SolutionPath = TestUtilities.GetSolutionPath();
    protected static readonly string ExampleFilePath = Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs");
    private static readonly string TestOutputRoot =
        Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "TestOutput");

    protected string TestOutputPath { get; }

    protected TestBase()
    {
        Directory.CreateDirectory(TestOutputRoot);
        TestOutputPath = Path.Combine(TestOutputRoot, Guid.NewGuid().ToString());
        Directory.CreateDirectory(TestOutputPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(TestOutputPath))
            Directory.Delete(TestOutputPath, true);
    }
}
