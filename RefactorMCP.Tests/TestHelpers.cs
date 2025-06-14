using System.IO;

namespace RefactorMCP.Tests;

public static class TestHelpers
{
    public static string GetSolutionPath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            var sln = Path.Combine(dir.FullName, "RefactorMCP.sln");
            if (File.Exists(sln)) return sln;
            dir = dir.Parent;
        }
        return "./RefactorMCP.sln";
    }

    public static string CreateTestOutputDir(string subfolder)
    {
        var path = Path.Combine(Path.GetDirectoryName(GetSolutionPath())!,
            "RefactorMCP.Tests", "TestOutput", subfolder);
        Directory.CreateDirectory(path);
        return path;
    }
}
