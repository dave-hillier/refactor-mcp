using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

[McpServerToolType]
public static class ListTools
{
    [McpServerTool, Description("List all available refactoring tools")]
    public static string ListToolsCommand()
        => RefactoringToolCatalog.FormatOperationGroups();
}
