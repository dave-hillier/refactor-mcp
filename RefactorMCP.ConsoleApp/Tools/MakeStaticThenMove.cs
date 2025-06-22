using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using RefactorMCP.ConsoleApp.Move;

[McpServerToolType]
public static class MakeStaticThenMoveTool
{
    [McpServerTool, Description("Convert an instance method to static and move it to another class (preferred for large C# file refactoring)")]
    public static async Task<string> MakeStaticThenMove(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Name of the method to convert and move")] string methodName,
        [Description("Name of the target class")] string targetClass,
        [Description("Name for the instance parameter (optional)")] string instanceParameterName = "instance",
        [Description("Path to the target file (optional, will create if doesn't exist or unspecified)")] string? targetFilePath = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await ConvertToStaticWithInstanceTool.ConvertToStaticWithInstance(
            solutionPath,
            filePath,
            methodName,
            instanceParameterName);

        return await MoveMethodTool.MoveStaticMethod(
            solutionPath,
            filePath,
            methodName,
            targetClass,
            targetFilePath,
            progress,
            cancellationToken);
    }
}
