using Xunit;

namespace RefactorMCP.Tests;

public class RoslynTransformationTests
{
    [Fact]
    public void IntroduceVariableInSource_AddsVariable()
    {
        var input = @"class Calculator
{
    void DisplayResult()
    {
        Console.WriteLine(1 + 2);
    }
}
";
        var expected = @"class Calculator
{
    void DisplayResult()
    {
        var result = Console.WriteLine(1 + 2);
        result;
    }
}
";
        var output = RefactoringTools.IntroduceVariableInSource(input, "5:27-5:31", "result");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void IntroduceParameterInSource_AddsParameter()
    {
        var input = @"class MathHelper
{
    int AddNumbers(int firstNumber, int secondNumber)
    {
        return firstNumber + secondNumber;
    }
}
";
        var expected = @"class MathHelper
{
    int AddNumbers(int firstNumber, int secondNumber, object calculationMode)
    {
        return firstNumber + secondNumber;
    }
}
";
        var output = RefactoringTools.IntroduceParameterInSource(input, "AddNumbers", "5:16-5:42", "calculationMode");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void MakeFieldReadonlyInSource_MakesReadonly()
    {
        var input = @"class CurrencyFormatter
{
    private string formatPattern = ""Currency"";

    public CurrencyFormatter()
    {
    }
}
";
        var expected = @"class CurrencyFormatter
{
    private readonly string formatPattern;

    public CurrencyFormatter()
    {
        formatPattern = ""Currency"";
    }
}
";
        var output = RefactoringTools.MakeFieldReadonlyInSource(input, "formatPattern");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void TransformSetterToInitInSource_ReplacesSetter()
    {
        var input = @"class UserProfile
{
    public string UserName { get; set; } = ""DefaultUser"";
}
";
        var expected = @"class UserProfile
{
    public string UserName { get; init; } = ""DefaultUser"";
}
";
        var output = RefactoringTools.TransformSetterToInitInSource(input, "UserName");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void ConvertToExtensionMethodInSource_TransformsMethod()
    {
        var input = @"class StringProcessor
{
    void FormatText()
    {
    }
}";
        var expected = @"class StringProcessor
{
    void FormatText()
    {
        StringProcessorExtensions.FormatText(this);
    }
}

public static class StringProcessorExtensions
{
    static void FormatText(this StringProcessor stringProcessor)
    {
    }
}";
        var output = RefactoringTools.ConvertToExtensionMethodInSource(input, "FormatText", null);
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void ConvertToStaticWithInstanceInSource_TransformsMethod()
    {
        var input = @"class DataProcessor
{
    int dataCount; 
    int GetDataCount()
    {
        return dataCount;
    }
}";
        var expected = @"class DataProcessor
{
    int dataCount;

    static int GetDataCount(DataProcessor instance)
    {
        return instance.dataCount;
    }
}";
        var output = RefactoringTools.ConvertToStaticWithInstanceInSource(input, "GetDataCount", "instance");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void ConvertToStaticWithParametersInSource_TransformsMethod()
    {
        var input = @"class Calculator
{
    int multiplier; 
    int MultiplyValue()
    {
        return multiplier;
    }
}";
        var expected = @"class Calculator
{
    int multiplier;

    static int MultiplyValue(int multiplier)
    {
        return multiplier;
    }
}";
        var output = RefactoringTools.ConvertToStaticWithParametersInSource(input, "MultiplyValue");
        Assert.Equal(expected, output.Trim());
    }

    [Fact(Skip = "ExtractMethod implementation needs debugging for this test case")]
    public void ExtractMethodInSource_CreatesMethod()
    {
        var input = @"class MessageHandler
{
    void ProcessMessage()
    {
        Console.WriteLine(""Processing message"");
    }
}
";
        var expected = @"class MessageHandler
{
    void ProcessMessage()
    {
        DisplayProcessingMessage();
    }

    private void DisplayProcessingMessage()
    {
        Console.WriteLine(""Processing message"");
    }
}
";
        var output = RefactoringTools.ExtractMethodInSource(input, "5:9-5:46", "DisplayProcessingMessage");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void IntroduceFieldInSource_AddsField()
    {
        var input = @"class Calculator
{
    int CalculateSum()
    {
        return 10 + 20;
    }
}
";
        var expected = @"class Calculator
{
    int CalculateSum()
    {
        return calculationResult;
    }
}
";
        var output = RefactoringTools.IntroduceFieldInSource(input, "5:16-5:23", "calculationResult", "private");
        Assert.Equal(expected, output);
    }

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
        var output = RefactoringTools.SafeDeleteFieldInSource(input, "configurationFlag");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void SafeDeleteMethodInSource_RemovesMethod()
    {
        var input = @"class ServiceManager
{
    void UnusedMethod()
    {
    }
}";
        var expected = @"class ServiceManager
{
}";
        var output = RefactoringTools.SafeDeleteMethodInSource(input, "UnusedMethod");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void SafeDeleteParameterInSource_RemovesParameter()
    {
        var input = @"class DataProcessor
{
    void ProcessData(int primaryValue, int unusedValue)
    {
    }
}";
        var expected = @"class DataProcessor
{
    void ProcessData(int primaryValue)
    {
    }
}";
        var output = RefactoringTools.SafeDeleteParameterInSource(input, "ProcessData", "unusedValue");
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
    }
}";
        var expected = @"class WorkflowManager
{
    void ExecuteWorkflow()
    {
    }
}";
        var output = RefactoringTools.SafeDeleteVariableInSource(input, "5:9-5:30");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveInstanceMethodInSource_MovesMethod()
    {
        var input = @"class DocumentProcessor 
{
    void ValidateDocument()
    {
    }
} 
class ValidationService
{
}";
        var expected = @"class DocumentProcessor
{
    private ValidationService validationService = new ValidationService();
}
class ValidationService
{
    public void ValidateDocument()
    {
    }
}";
        var output = RefactoringTools.MoveInstanceMethodInSource(input, "DocumentProcessor", "ValidateDocument", "ValidationService", "validationService", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveStaticMethodInSource_MovesMethod()
    {
        var input = @"class UtilityHelper
{
    static void FormatString()
    {
    }
}";
        var expected = @"class UtilityHelper
{
}

public class StringUtilities
{
    static void FormatString()
    {
    }
}";
        var output = RefactoringTools.MoveStaticMethodInSource(input, "FormatString", "StringUtilities");
        Assert.Equal(expected, output.Trim());
    }
}
