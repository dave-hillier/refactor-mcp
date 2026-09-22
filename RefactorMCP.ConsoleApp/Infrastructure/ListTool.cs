using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Linq;

[McpServerToolType]
public static class ListToolsTool
{
    [McpServerTool, Description("List all available refactoring tools")]
    public static string ListTools()
        => string.Join('\n', ToolDispatcher.Default.ListTools().Select(tool => tool.Name));
}
