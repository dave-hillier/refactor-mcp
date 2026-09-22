using System;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// The command line surface: run a tool, list the tools, manage daemons, or
/// serve the tools over MCP.
/// </summary>
internal static class Cli
{
    /// <summary>Name the user typed to run this program, for usage messages.</summary>
    public static string ProgramName
    {
        get
        {
            var path = Environment.ProcessPath;
            var name = path is null ? null : Path.GetFileNameWithoutExtension(path);
            return string.IsNullOrEmpty(name) || name == "dotnet" ? "refactor" : name;
        }
    }

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] == "mcp")
            return await McpCommand.RunAsync(args);

        switch (args[0])
        {
            case "--help" or "-h" or "help":
                Console.WriteLine(Usage);
                return 0;
            case "--json":
                return await ToolCommand.RunJsonAsync(args);
            case "--cli":
                return await ToolCommand.RunAsync(args[1..]);
            case "serve":
                return await ServeCommand.RunAsync(args);
            case "status":
                return StatusCommand.Show();
            case "stop":
                return StatusCommand.Stop(args);
            case "list-tools":
                return ListToolsCommand.Run(args);
        }

        if (ToolDispatcher.Default.Resolve(args[0]) is not null)
            return await ToolCommand.RunAsync(args);

        Console.Error.WriteLine($"Unknown command or tool: {args[0]}");
        Console.Error.WriteLine();
        Console.Error.WriteLine(Usage);
        return 1;
    }

    public static string Usage => $"""
        Usage:
          {ProgramName} <tool> [--option value] [arguments]   run a refactoring tool
          {ProgramName} --json <tool> '<params>'              run a tool with JSON parameters
          {ProgramName} list-tools [--verbose]                list the available tools
          {ProgramName} serve --solution <path> [--idle-timeout 10m]
                                                              keep a solution loaded and serve calls
          {ProgramName} status                                show running daemons
          {ProgramName} stop [--solution <path> | --all]      shut daemons down
          {ProgramName} mcp                                   serve the tools over MCP on stdio

        Options:
          --no-daemon    run the tool in this process instead of through a daemon
          --help         show this message, or the options of a tool

        Run '{ProgramName} list-tools --verbose' to see every tool with its description.
        """;
}
