using System;

/// <summary>
/// The <c>list-tools</c> verb: the tool surface, from the tool metadata, without
/// loading Roslyn.
/// </summary>
internal static class ListToolsCommand
{
    public static int Run(string[] args)
    {
        var verbose = ParsedCommand.Parse(args).HasOption("verbose");

        foreach (var tool in ToolDispatcher.Default.ListTools())
        {
            Console.WriteLine(verbose
                ? $"{tool.Name} - {tool.Description}{(tool.IsPrompt ? " (prompt)" : string.Empty)}"
                : tool.Name);
        }

        return 0;
    }
}
