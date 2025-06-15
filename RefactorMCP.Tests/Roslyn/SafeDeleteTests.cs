using Xunit;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void SafeDeleteFieldInSource_RemovesField()
    {
        var input = @"class ConfigurationManager
{
    int configurationFlag;
}";
        var expected = @"class ConfigurationManager
{
}";
        var output = SafeDeleteTool.SafeDeleteFieldInSource(input, "configurationFlag");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void SafeDeleteMethodInSource_RemovesMethod()
    {
        var input = @"class ServiceManager
{
    void UnusedMethod()
    {
        Console.WriteLine(""Hello"");
    }
}";
        var expected = @"class ServiceManager
{
}";
        var output = SafeDeleteTool.SafeDeleteMethodInSource(input, "UnusedMethod");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void SafeDeleteParameterInSource_RemovesParameter()
    {
        var input = @"class DataProcessor
{
    void ProcessData(int primaryValue, int unusedValue)
    {
        Console.WriteLine(primaryValue);
    }
}";
        var expected = @"class DataProcessor
{
    void ProcessData(int primaryValue)
    {
        Console.WriteLine(primaryValue);
    }
}";
        var output = SafeDeleteTool.SafeDeleteParameterInSource(input, "ProcessData", "unusedValue");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void SafeDeleteVariableInSource_RemovesVariable()
    {
        var input = @"class WorkflowManager
{
    void ExecuteWorkflow()
    {
        int unusedCounter = 1;
        Console.WriteLine(""done"");
    }
}";
        var expected = @"class WorkflowManager
{
    void ExecuteWorkflow()
    {
        Console.WriteLine(""done"");
    }
}";
        var output = SafeDeleteTool.SafeDeleteVariableInSource(input, "5:9-5:30");
        Assert.Equal(expected, output.Trim());
    }

}
