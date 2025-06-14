using ModelContextProtocol;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using Xunit;

namespace RefactorMCP.Tests;

public class CliIntegrationTests
{
    private static string GetSolutionPath() => TestHelpers.GetSolutionPath();

    [Fact]
    public async Task CliTestMode_LoadSolution_WorksCorrectly()
    {
        var result = await LoadSolutionTool.LoadSolution(GetSolutionPath());
        Assert.Contains("Successfully loaded solution", result);
    }

    [Fact]
    public async Task CliTestMode_AllToolsListed_ReturnsExpectedTools()
    {
        var expectedCommands = new[]
        {
            "list-tools",
            "load-solution",
            "extract-method",
            "introduce-field",
            "introduce-variable",
            "make-field-readonly",
            "unload-solution",
            "clear-solution-cache",
            "convert-to-extension-method",
            "convert-to-static-with-parameters",
            "convert-to-static-with-instance",
            "introduce-parameter",
            "move-static-method",
            "move-instance-method",
            "transform-setter-to-init",
            "safe-delete-field",
            "safe-delete-method",
            "safe-delete-parameter",
            "safe-delete-variable",
            "version"
        };

        var refactoringMethods = typeof(LoadSolutionTool).Assembly
            .GetTypes()
            .Where(t => t.GetCustomAttributes(typeof(McpServerToolTypeAttribute), false).Length > 0)
            .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            .ToArray();

        foreach (var command in expectedCommands)
        {
            if (command == "list-tools")
            {
                var progType = typeof(LoadSolutionTool).Assembly.GetType("Program");
                Assert.NotNull(progType);
                var progMethod = progType!.GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .FirstOrDefault(m => m.Name.Contains("ListAvailableTools"));
                Assert.NotNull(progMethod);
                continue;
            }

            var pascal = string.Concat(command
                .Split('-')
                .Select(w => char.ToUpper(w[0]) + w[1..]));

            var method = refactoringMethods.FirstOrDefault(m =>
                m.Name.Equals(pascal, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(method);
        }
    }
}
