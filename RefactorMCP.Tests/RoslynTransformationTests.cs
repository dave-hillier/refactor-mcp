using Xunit;

namespace RefactorMCP.Tests;

    public class RoslynTransformationTests
{
    [Fact]
    public void IntroduceVariableInSource_AddsVariable()
    {
        var input = @"class Calculator
{
    int Calculate()
    {
        return (1 + 2) * 3;
    }
}";
        var expected = @"class Calculator
{
    int Calculate()
    {
        var sum = 1 + 2;
        return sum * 3;
    }
}";
        var output = IntroduceVariableTool.IntroduceVariableInSource(input, "5:17-5:21", "sum");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void IntroduceVariableInSource_ReplacesAllOccurrences()
    {
        var input = @"class Calculator
{
    int Calculate()
    {
        return (1 + 2) * (1 + 2);
    }
}";
        var expected = @"class Calculator
{
    int Calculate()
    {
        var sum = 1 + 2;
        return sum * sum;
    }
}";
        var output = IntroduceVariableTool.IntroduceVariableInSource(input, "5:17-5:21", "sum");
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
        var output = IntroduceParameterTool.IntroduceParameterInSource(input, "AddNumbers", "5:16-5:42", "calculationMode");
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
        Console.WriteLine(formatPattern);
    }
}
";
        var expected = @"class CurrencyFormatter
{
    private readonly string formatPattern;

    public CurrencyFormatter()
    {
        Console.WriteLine(formatPattern);
        formatPattern = ""Currency"";
    }
}
";
        var output = MakeFieldReadonlyTool.MakeFieldReadonlyInSource(input, "formatPattern");
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
        var output = TransformSetterToInitTool.TransformSetterToInitInSource(input, "UserName");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void ConvertToExtensionMethodInSource_TransformsMethod()
    {
        var input = @"class StringProcessor
{
    void FormatText()
    {
        Console.WriteLine(""Hello"");
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
        Console.WriteLine(""Hello"");
    }
}";
        var output = ConvertToExtensionMethodTool.ConvertToExtensionMethodInSource(input, "FormatText", null);
Assert.Equal(expected, output.Trim());
    }
    [Fact]
    public void ConvertToExtensionMethodInSource_AppendsToExistingClass()
{
    var input = @"class StringProcessor
{
    void FormatText()
    {
        Console.WriteLine(""Hello"");
    }
}

public static class StringProcessorExtensions
{
}
";
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
        Console.WriteLine(""Hello"");
    }
}";
        var output = ConvertToExtensionMethodTool.ConvertToExtensionMethodInSource(input, "FormatText", "StringProcessorExtensions");
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
    var output = ConvertToStaticWithInstanceTool.ConvertToStaticWithInstanceInSource(input, "GetDataCount", "instance");
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
    var output = ConvertToStaticWithParametersTool.ConvertToStaticWithParametersInSource(input, "MultiplyValue");
    Assert.Equal(expected, output.Trim());
}

    [Fact]
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
    var output = ExtractMethodTool.ExtractMethodInSource(input, "5:9-5:49", "DisplayProcessingMessage");
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
    var output = IntroduceFieldTool.IntroduceFieldInSource(input, "5:16-5:23", "calculationResult", "private");
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
    [Fact]
    public void MoveInstanceMethodInSource_MovesMethod()
{
    var input = @"class DocumentProcessor 
{
    void ValidateDocument()
    {
        Console.WriteLine(""Validating"");
    }
} 
class ValidationService
{
}
";
        var expected = @"class DocumentProcessor
{
    private ValidationService validationService = new ValidationService();

    void ValidateDocument()
    {
        validationService.ValidateDocument();
    }
}
class ValidationService
{
    public void ValidateDocument()
    {
        Console.WriteLine(""Validating"");
    }
}";
        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "DocumentProcessor", "ValidateDocument", "ValidationService", "validationService", "field");
Assert.Equal(expected, output.Trim());
    }
    [Fact]
    public void MoveInstanceMethodInSource_PropertyTargetExists()
{
    var input = @"class TaskProcessor
{
    void RunTask()
    {
        Console.WriteLine(""Running"");
    }
}
class TaskRunner
{
}
";
        var expected = @"class TaskProcessor
{
    private TaskRunner Runner { get; set; }

    void RunTask()
    {
        Runner.RunTask();
    }
}
class TaskRunner
{
    public void RunTask()
    {
        Console.WriteLine(""Running"");
    }
}";
        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "TaskProcessor", "RunTask", "TaskRunner", "Runner", "property");
Assert.Equal(expected, output.Trim());
    }
    [Fact]
    public void MoveInstanceMethodInSource_CreatesTargetClass()
{
    var input = @"class Calculator
{
    void Compute()
    {
        Console.WriteLine(""Compute"");
    }
}";
        var expected = @"class Calculator
{
    private Logger logger = new Logger();

    void Compute()
    {
        logger.Compute();
    }
}

public class Logger
{
    public void Compute()
    {
        Console.WriteLine(""Compute"");
    }
}";
        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "Calculator", "Compute", "Logger", "logger", "field");
Assert.Equal(expected, output.Trim());
    }
    [Fact]
    public void MoveStaticMethodInSource_MovesMethod()
{
    var input = @"class UtilityHelper
{
    static void FormatString()
    {
        Console.WriteLine(""Formatting"");
    }
}";
        var expected = @"class UtilityHelper
{
    static void FormatString()
    {
        StringUtilities.FormatString();
    }
}

public class StringUtilities
{
    static void FormatString()
    {
        Console.WriteLine(""Formatting"");
    }
}";
        var output = MoveMethodsTool.MoveStaticMethodInSource(input, "FormatString", "StringUtilities");
Assert.Equal(expected, output.Trim());
    }
    [Fact]
    public void MoveStaticMethodInSource_TargetClassExists()
{
    var input = @"class UtilityHelper
{
    static void FormatString()
    {
        Console.WriteLine(""Formatting"");
    }
}

class StringUtilities
{
}
";
        var expected = @"class UtilityHelper
{
    static void FormatString()
    {
        StringUtilities.FormatString();
    }
}

class StringUtilities
{
    static void FormatString()
    {
        Console.WriteLine(""Formatting"");
    }
}";
        var output = MoveMethodsTool.MoveStaticMethodInSource(input, "FormatString", "StringUtilities");
Assert.Equal(expected, output.Trim());
    }
    [Fact]
    public void MoveInstanceMethodInSource_GetAverageToMathUtilities()
{
    var input = @"class Calculator
{
    private List<int> numbers = new List<int>();

    public double GetAverage()
    {
        return numbers.Sum() / (double)numbers.Count;
    }
}

class MathUtilities
{
}";
    var expected = @"class Calculator
{
    private List<int> numbers = new List<int>();
    private MathUtilities mathUtilities = new MathUtilities();

    public double GetAverage()
    {
        return mathUtilities.GetAverage(this);
    }
}

class MathUtilities
{
    public double GetAverage(Calculator calculator)
    {
        return calculator.numbers.Sum() / (double)calculator.numbers.Count;
    }
}";
    var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "Calculator", "GetAverage", "MathUtilities", "mathUtilities", "field");
    Assert.Equal(expected, output.Trim());
}
}
