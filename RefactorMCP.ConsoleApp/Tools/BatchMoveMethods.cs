using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
public static class BatchMoveMethodsTool
{
    [McpServerTool, Description("Move methods in batch using JSON operations")]
    public static async Task<string> BatchMoveMethods(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the methods")] string filePath,
        [Description("JSON array describing the move operations")] string operationsJson,
        [Description("Default target file path used when operations omit targetFile (optional)")] string? defaultTargetFilePath = null)
    {
        return await MoveMultipleMethodsTool.MoveMultipleMethods(solutionPath, filePath, operationsJson, defaultTargetFilePath);
    }
}
