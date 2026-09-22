using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

/// <summary>
/// The <c>mcp</c> verb (also what runs when the program is started with no
/// arguments): serve the tools over the Model Context Protocol on stdio.
/// </summary>
internal static class McpCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        // The MCP server is long lived, so its sessions follow the files on
        // disk rather than working from the text they loaded.
        SessionRegistry.WatchForFileChanges = true;

        // `mcp` is the command, not host configuration.
        var hostArgs = args.Length > 0 && args[0] == "mcp" ? args[1..] : args;

        var builder = Host.CreateApplicationBuilder(hostArgs);
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            // Configure all logs to go to stderr
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly()
            .WithResourcesFromAssembly()
            .WithPromptsFromAssembly();

        await builder.Build().RunAsync();
        return 0;
    }
}
