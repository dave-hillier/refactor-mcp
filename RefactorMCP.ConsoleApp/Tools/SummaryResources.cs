using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using ModelContextProtocol.Server;

[McpServerResourceType]
public static class SummaryResources
{
    [McpServerResource(UriTemplate = "summary://{*file}", MimeType = "text/plain")]
    public static async Task<string> GetSummary(string file, CancellationToken cancellationToken = default)
    {
        var normalized = file.Replace('/', Path.DirectorySeparatorChar);
        if (!File.Exists(normalized))
        {
            return $"// File not found: {file}";
        }

        var text = await File.ReadAllTextAsync(normalized, cancellationToken);
        var tree = CSharpSyntaxTree.ParseText(text);
        var root = await tree.GetRootAsync(cancellationToken);
        var summarized = new BodyOmitter().Visit(root);
        var workspace = new AdhocWorkspace();
        var formatted = Formatter.Format(summarized, workspace);

        var sb = new StringBuilder();
        sb.AppendLine($"// summary://{file}");
        sb.AppendLine("// This file omits method bodies for brevity.");
        sb.Append(formatted.ToFullString());
        return sb.ToString();
    }

}
