using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;
using System.ComponentModel;

public static partial class RefactoringTools
{
    [McpServerTool, Description("Move a static method to another class")]
    public static async Task<string> MoveStaticMethod(
        [Description("Path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Line number of the static method to move")] int methodLine,
        [Description("Name of the target class")] string targetClass,
        [Description("Path to the target file (optional, will create if doesn't exist)")] string? targetFilePath = null)
    {
        // TODO: Implement move static method refactoring using Roslyn
        return $"Move static method at line {methodLine} from {filePath} to class '{targetClass}' - Implementation in progress";
    }
}
