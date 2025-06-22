using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

[McpServerResourceType]
public static class MetricsResource
{
    [McpServerResource(UriTemplate = "metrics://{+path}")]
    [Description("Return code metrics for directories, files, classes or methods")]
    public static async Task<TextResourceContents> ReadMetrics(
        [Description("Target path within the solution")] string path,
        [Description("Absolute path to the solution file (.sln)")] string solutionPath)
    {
        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            var classes = new List<JsonElement>();
            foreach (var file in Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories))
            {
                using var metrics = await GetFileMetricsJson(solutionPath, file);
                if (metrics.RootElement.TryGetProperty("classes", out var clsArray))
                {
                    classes.AddRange(clsArray.EnumerateArray().Select(c => c.Clone()));
                }
            }
            var json = JsonSerializer.Serialize(classes, new JsonSerializerOptions { WriteIndented = true });
            return new TextResourceContents { Text = json };
        }

        var className = (string?)null;
        var methodName = (string?)null;
        if (!File.Exists(fullPath))
        {
            var slash = fullPath.LastIndexOf(Path.DirectorySeparatorChar);
            if (slash >= 0)
            {
                var tail = fullPath[(slash + 1)..];
                var potentialFile = fullPath[..slash];
                if (File.Exists(potentialFile))
                {
                    fullPath = potentialFile;
                    var dot = tail.IndexOf('.');
                    if (dot >= 0)
                    {
                        className = tail[..dot];
                        methodName = tail[(dot + 1)..];
                    }
                    else
                    {
                        className = tail;
                    }
                }
            }
        }

        using var metricsJson = await GetFileMetricsJson(solutionPath, fullPath);
        object result;
        if (className == null)
        {
            result = metricsJson.RootElement.Clone();
        }
        else if (metricsJson.RootElement.TryGetProperty("classes", out var classes))
        {
            var cls = classes.EnumerateArray().FirstOrDefault(c => c.GetProperty("name").GetString() == className);
            if (cls.ValueKind == JsonValueKind.Undefined)
            {
                result = new { Error = $"Class {className} not found" };
            }
            else if (methodName == null)
            {
                result = cls.Clone();
            }
            else
            {
                var method = cls.GetProperty("methods").EnumerateArray().FirstOrDefault(m => m.GetProperty("name").GetString() == methodName);
                result = method.ValueKind == JsonValueKind.Undefined ? new { Error = $"Method {methodName} not found" } : method.Clone();
            }
        }
        else
        {
            result = new { Error = $"Class {className} not found" };
        }

        var jsonText = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        return new TextResourceContents { Text = jsonText };
    }

    private static async Task<JsonDocument> GetFileMetricsJson(string solutionPath, string filePath)
    {
        var json = await MetricsProvider.GetFileMetrics(solutionPath, filePath);
        return JsonDocument.Parse(json);
    }
}
