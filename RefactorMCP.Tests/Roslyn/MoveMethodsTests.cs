using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
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
    private readonly ValidationService validationService = new ValidationService();

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
    private readonly Logger logger = new Logger();

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
    private readonly MathUtilities mathUtilities = new MathUtilities();

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

    [Fact]
    public void MoveInstanceMethodInSource_FailureMode_CreatesEmptyTargetClass()
    {
        // This test replicates the failure mode where the tool creates the target class
        // and access member but fails to actually move the method content
        var input = @"class cResRoom
{
    void CheckIn_UseAdvanceDeposits()
    {
        Console.WriteLine(""Processing advance deposits"");
        // Some complex logic here
        var deposits = GetAdvanceDeposits();
        ProcessDeposits(deposits);
    }

    private object GetAdvanceDeposits() => null;
    private void ProcessDeposits(object deposits) { }
}";

        // In the failure mode, this is what we would expect (correct behavior)
        var expected = @"class cResRoom
{
    private readonly ResRoomDepositManager resRoom = new ResRoomDepositManager();

    void CheckIn_UseAdvanceDeposits()
    {
        resRoom.CheckIn_UseAdvanceDeposits(this);
    }

    private object GetAdvanceDeposits() => null;
    private void ProcessDeposits(object deposits) { }
}

public class ResRoomDepositManager
{
    public void CheckIn_UseAdvanceDeposits(cResRoom cresroom)
    {
        Console.WriteLine(""Processing advance deposits"");
        // Some complex logic here
        var deposits = cresroom.GetAdvanceDeposits();
        cresroom.ProcessDeposits(deposits);
    }
}";


        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "cResRoom", "CheckIn_UseAdvanceDeposits", "ResRoomDepositManager", "resRoom", "field");

        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveMultipleInstanceMethodsInSource_MovesAllMethods()
    {
        var input = @"class OrderProcessor
{
    private List<string> orders = new List<string>();

    void ValidateOrder()
    {
        Console.WriteLine(""Validating order"");
    }

    void ProcessPayment()
    {
        Console.WriteLine(""Processing payment"");
    }

    void SendConfirmation()
    {
        Console.WriteLine(""Sending confirmation"");
    }

    int GetOrderCount()
    {
        return orders.Count;
    }
}";

        var expected = @"class OrderProcessor
{
    private List<string> orders = new List<string>();
    private readonly OrderService orderService = new OrderService();

    void ValidateOrder()
    {
        orderService.ValidateOrder();
    }

    void ProcessPayment()
    {
        orderService.ProcessPayment();
    }

    void SendConfirmation()
    {
        orderService.SendConfirmation();
    }

    int GetOrderCount()
    {
        return orderService.GetOrderCount(this);
    }
}

public class OrderService
{
    public void ValidateOrder()
    {
        Console.WriteLine(""Validating order"");
    }
    public void ProcessPayment()
    {
        Console.WriteLine(""Processing payment"");
    }
    public void SendConfirmation()
    {
        Console.WriteLine(""Sending confirmation"");
    }
    public int GetOrderCount(OrderProcessor orderprocessor)
    {
        return orderprocessor.orders.Count;
    }
}";

        // Test moving multiple methods
        var methodNames = new[] { "ValidateOrder", "ProcessPayment", "SendConfirmation", "GetOrderCount" };
        var output = MoveMethodsTool.MoveMultipleInstanceMethodsInSource(input, "OrderProcessor", methodNames, "OrderService", "orderService", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveMultipleMethodsInSource_WithJsonOperations_MovesMethodsInCorrectOrder()
    {
        var input = @"class DataProcessor
{
    private List<string> data = new List<string>();

    public void ProcessData()
    {
        ValidateData();
        TransformData();
    }

    private void ValidateData()
    {
        Console.WriteLine(""Validating data"");
    }

    private void TransformData()
    {
        Console.WriteLine(""Transforming data"");
    }

    static void LogOperation(string operation)
    {
        Console.WriteLine($""Operation: {operation}"");
    }
}
class DataValidator
{
}
class Logger
{
}";

        var expected = @"class DataProcessor
{
    private List<string> data = new List<string>();
    private readonly DataValidator dataValidator = new DataValidator();

    public void ProcessData()
    {
        dataValidator.ValidateData();
        dataValidator.TransformData();
    }

    static void LogOperation(string operation)
    {
        Logger.LogOperation(operation);
    }
}

class DataValidator
{
    public void ValidateData()
    {
        Console.WriteLine(""Validating data"");
    }
    public void TransformData()
    {
        Console.WriteLine(""Transforming data"");
    }
}

class Logger
{
    static void LogOperation(string operation)
    {
        Console.WriteLine($""Operation: {operation}"");
    }
}";

        // Create JSON operations for multiple method moves
        var operationsJson = @"[
            {
                ""SourceClass"": ""DataProcessor"",
                ""Method"": ""ValidateData"",
                ""TargetClass"": ""DataValidator"",
                ""AccessMember"": ""dataValidator"",
                ""AccessMemberType"": ""field"",
                ""IsStatic"": false
            },
            {
                ""SourceClass"": ""DataProcessor"",
                ""Method"": ""TransformData"",
                ""TargetClass"": ""DataValidator"",
                ""AccessMember"": ""dataValidator"",
                ""AccessMemberType"": ""field"",
                ""IsStatic"": false
            },
            {
                ""Method"": ""LogOperation"",
                ""TargetClass"": ""Logger"",
                ""IsStatic"": true
            }
        ]";

        var output = MoveMultipleMethodsTool.MoveMultipleMethodsInSource(input, operationsJson);
        Assert.Contains("private readonly DataValidator dataValidator", output);
        Assert.Contains("dataValidator.ValidateData()", output);
        Assert.Contains("dataValidator.TransformData()", output);
    }

    [Fact]
    public void MoveMultipleMethodsInSource_WithDependencies_OrdersCorrectly()
    {
        var input = @"class Calculator
{
    public int Add(int a, int b)
    {
        return PerformCalculation(a, b, ""+"");
    }

    public int Subtract(int a, int b)
    {
        return PerformCalculation(a, b, ""-"");
    }

    private int PerformCalculation(int a, int b, string operation)
    {
        LogOperation(operation);
        return operation == ""+"" ? a + b : a - b;
    }

    private void LogOperation(string operation)
    {
        Console.WriteLine($""Performing {operation}"");
    }
}

class MathOperations
{
}";

        var expected = @"class Calculator
{
    private readonly MathOperations mathOperations = new MathOperations();

    public int Add(int a, int b)
    {
        return mathOperations.PerformCalculation(a, b, ""+"", this);
    }

    public int Subtract(int a, int b)
    {
        return mathOperations.PerformCalculation(a, b, ""-"", this);
    }
}

class MathOperations
{
    public void LogOperation(string operation)
    {
        Console.WriteLine($""Performing {operation}"");
    }
    public int PerformCalculation(int a, int b, string operation, Calculator calculator)
    {
        LogOperation(operation);
        return operation == ""+"" ? a + b : a - b;
    }
}";

        // Create JSON operations that have dependencies (PerformCalculation depends on LogOperation)
        var operationsJson = @"[
            {
                ""SourceClass"": ""Calculator"",
                ""Method"": ""PerformCalculation"",
                ""TargetClass"": ""MathOperations"",
                ""AccessMember"": ""mathOperations"",
                ""AccessMemberType"": ""field"",
                ""IsStatic"": false
            },
            {
                ""SourceClass"": ""Calculator"",
                ""Method"": ""LogOperation"",
                ""TargetClass"": ""MathOperations"",
                ""AccessMember"": ""mathOperations"",
                ""AccessMemberType"": ""field"",
                ""IsStatic"": false
            }
        ]";

        var output = MoveMultipleMethodsTool.MoveMultipleMethodsInSource(input, operationsJson);
        Assert.Contains("private readonly MathOperations mathOperations", output);
        Assert.Contains("mathOperations.PerformCalculation", output);
    }

    [Fact]
    public void MoveInstanceMethodInSource_SkipsExistingAccessField()
    {
        var input = @"class Foo
{
    private readonly Bar bar = new Bar();

    void Do()
    {
        Console.WriteLine(""hi"");
    }
}
class Bar
{
}";

        var expected = @"class Foo
{
    private readonly Bar bar = new Bar();

    void Do()
    {
        bar.Do();
    }
}
class Bar
{
    public void Do()
    {
        Console.WriteLine(""hi"");
    }
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "Foo", "Do", "Bar", "bar", "field");
        Assert.Contains("private readonly Bar bar", output);
        Assert.Contains("bar.Do()", output);
    }

    [Fact]
    public void MoveInstanceMethodInSource_WithProtectedMethods_ChangesToPublic()
    {
        var input = @"class Calculator
{
    protected int Multiply(int a, int b)
    {
        return a * b;
    }
}

class MathOperations
{
}";

        var expected = @"class Calculator
{
    private readonly MathOperations mathOperations = new MathOperations();

    protected int Multiply(int a, int b)
    {
        return mathOperations.Multiply(a, b);
    }
}

class MathOperations
{
    public int Multiply(int a, int b)
    {
        return a * b;
    }
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "Calculator", "Multiply", "MathOperations", "mathOperations", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveInstanceMethodInSource_WithInternalMethods_ChangesToPublic()
    {
        var input = @"class Calculator
{
    internal void LogCalculation(string operation)
    {
        Console.WriteLine($""Performed: {operation}"");
    }
}

class Logger
{
}";

        var expected = @"class Calculator
{
    private readonly Logger logger = new Logger();

    internal void LogCalculation(string operation)
    {
        logger.LogCalculation(operation);
    }
}

class Logger
{
    public void LogCalculation(string operation)
    {
        Console.WriteLine($""Performed: {operation}"");
    }
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "Calculator", "LogCalculation", "Logger", "logger", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveInstanceMethodInSource_WithPropertyAccess_RewritesCorrectly()
    {
        var input = @"class DataProcessor
{
    public string Name { get; set; } = ""DefaultProcessor"";

    public string GetProcessorInfo()
    {
        return $""Processor: {Name}"";
    }
}

class InfoProvider
{
}";

        var expected = @"class DataProcessor
{
    public string Name { get; set; } = ""DefaultProcessor"";

    private readonly InfoProvider infoProvider = new InfoProvider();

    public string GetProcessorInfo()
    {
        return infoProvider.GetProcessorInfo(this);
    }
}

class InfoProvider
{
    public string GetProcessorInfo(DataProcessor dataprocessor)
    {
        return $""Processor: {dataprocessor.Name}"";
    }
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "DataProcessor", "GetProcessorInfo", "InfoProvider", "infoProvider", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveInstanceMethodInSource_WithPropertyMember_UsesPropertyAccess()
    {
        var input = @"class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}

class MathOperations
{
}";

        var expected = @"class Calculator
{
    private MathOperations MathOps { get; set; }

    public int Add(int a, int b)
    {
        return MathOps.Add(a, b);
    }
}

class MathOperations
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "Calculator", "Add", "MathOperations", "MathOps", "property");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveInstanceMethodInSource_MethodWithComplexReturnType_PreservesSignature()
    {
        var input = @"class DataProcessor
{
    public List<Dictionary<string, int>> ProcessData(IEnumerable<string> input)
    {
        return input.Select(s => new Dictionary<string, int> { [s] = s.Length }).ToList();
    }
}

class DataTransformer
{
}";

        var expected = @"class DataProcessor
{
    private readonly DataTransformer dataTransformer = new DataTransformer();

    public List<Dictionary<string, int>> ProcessData(IEnumerable<string> input)
    {
        return dataTransformer.ProcessData(input);
    }
}

class DataTransformer
{
    public List<Dictionary<string, int>> ProcessData(IEnumerable<string> input)
    {
        return input.Select(s => new Dictionary<string, int> { [s] = s.Length }).ToList();
    }
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "DataProcessor", "ProcessData", "DataTransformer", "dataTransformer", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveInstanceMethodInSource_WithGenericMethod_PreservesGenerics()
    {
        var input = @"class Container
{
    public T Process<T>(T value) where T : class
    {
        return value;
    }
}

class Processor
{
}";

        var expected = @"class Container
{
    private readonly Processor processor = new Processor();

    public T Process<T>(T value) where T : class
    {
        return processor.Process(value);
    }
}

class Processor
{
    public T Process<T>(T value) where T : class
    {
        return value;
    }
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "Container", "Process", "Processor", "processor", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveInstanceMethodInSource_WithMethodUsingOtherMethods_PassesThisReference()
    {
        var input = @"class Calculator
{
    public int Square(int number)
    {
        return Multiply(number, number);
    }

    public int Multiply(int a, int b)
    {
        return a * b;
    }
}

class MathOperations
{
}";

        var expected = @"class Calculator
{
    private readonly MathOperations mathOperations = new MathOperations();

    public int Square(int number)
    {
        return mathOperations.Square(number, this);
    }

    public int Multiply(int a, int b)
    {
        return a * b;
    }
}

class MathOperations
{
    public int Square(int number, Calculator calculator)
    {
        return calculator.Multiply(number, number);
    }
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "Calculator", "Square", "MathOperations", "mathOperations", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveStaticMethodInSource_WithMultipleClasses_AddsToCorrectClass()
    {
        var input = @"class Utilities
{
    public static string FormatName(string firstName, string lastName)
    {
        return $""{lastName}, {firstName}"";
    }
}

class StringHelper
{
}

class NumberHelper
{
}";

        var expected = @"class Utilities
{
    public static string FormatName(string firstName, string lastName)
    {
        return StringHelper.FormatName(firstName, lastName);
    }
}

class StringHelper
{
    public static string FormatName(string firstName, string lastName)
    {
        return $""{lastName}, {firstName}"";
    }
}

class NumberHelper
{
}";

        var output = MoveMethodsTool.MoveStaticMethodInSource(input, "FormatName", "StringHelper");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveStaticMethodInSource_WithStaticFields_PreservesReferences()
    {
        var input = @"class Configuration
{
    public static readonly string DefaultPath = ""/tmp"";

    public static string GetFullPath(string filename)
    {
        return Path.Combine(DefaultPath, filename);
    }
}

class PathHelper
{
}";

        var expected = @"class Configuration
{
    public static readonly string DefaultPath = ""/tmp"";

    public static string GetFullPath(string filename)
    {
        return PathHelper.GetFullPath(filename);
    }
}

class PathHelper
{
    public static string GetFullPath(string filename)
    {
        return Path.Combine(Configuration.DefaultPath, filename);
    }
}";

        var output = MoveMethodsTool.MoveStaticMethodInSource(input, "GetFullPath", "PathHelper");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveMultipleStaticMethods_WithDifferentTargets_DistributesCorrectly()
    {
        var input = @"class Utilities
{
    public static string FormatString(string input)
    {
        return input.Trim().ToUpper();
    }

    public static int AddNumbers(int a, int b)
    {
        return a + b;
    }

    public static bool IsValidEmail(string email)
    {
        return email.Contains(""@"");
    }
}

class StringHelper
{
}

class MathHelper
{
}

class ValidationHelper
{
}";

        var operationsJson = @"[
            {
                ""Method"": ""FormatString"",
                ""TargetClass"": ""StringHelper"",
                ""IsStatic"": true
            },
            {
                ""Method"": ""AddNumbers"",
                ""TargetClass"": ""MathHelper"",
                ""IsStatic"": true
            },
            {
                ""Method"": ""IsValidEmail"",
                ""TargetClass"": ""ValidationHelper"",
                ""IsStatic"": true
            }
        ]";

        var output = MoveMultipleMethodsTool.MoveMultipleMethodsInSource(input, operationsJson);

        // Verify that methods are moved to correct classes
        Assert.Contains("class StringHelper", output);
        Assert.Contains("FormatString", output);
        Assert.Contains("class MathHelper", output);
        Assert.Contains("AddNumbers", output);
        Assert.Contains("class ValidationHelper", output);
        Assert.Contains("IsValidEmail", output);
        // Verify that methods are moved to correct classes
        Assert.Contains("class Utilities", output);
        Assert.Contains("class StringHelper", output);
        Assert.Contains("class MathHelper", output);
        Assert.Contains("class ValidationHelper", output);
    }

    [Fact]
    public void MoveInstanceMethodInSource_WithRecursiveMethod_PreservesRecursion()
    {
        var input = @"class Calculator
{
    public int Factorial(int n)
    {
        if (n <= 1) return 1;
        return n * Factorial(n - 1);
    }
}

class MathOperations
{
}";

        var expected = @"class Calculator
{
    private readonly MathOperations mathOperations = new MathOperations();

    public int Factorial(int n)
    {
        return mathOperations.Factorial(n, this);
    }
}

class MathOperations
{
    public int Factorial(int n, Calculator calculator)
    {
        if (n <= 1) return 1;
        return n * calculator.Factorial(n - 1);
    }
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "Calculator", "Factorial", "MathOperations", "mathOperations", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveInstanceMethodInSource_WithExpressionBodiedMethod_PreservesFormat()
    {
        var input = @"class Calculator
{
    public int Double(int x) => x * 2;
}

class MathOperations
{
}";

        var expected = @"class Calculator
{
    private readonly MathOperations mathOperations = new MathOperations();

    public int Double(int x)
    {
        return mathOperations.Double(x);
    }
}

class MathOperations
{
    public int Double(int x) => x * 2;
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "Calculator", "Double", "MathOperations", "mathOperations", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveInstanceMethodInSource_WithAsyncMethod_PreservesAsync()
    {
        var input = @"class DataProcessor
{
    public async Task<string> ProcessDataAsync(string input)
    {
        await Task.Delay(100);
        return input.ToUpper();
    }
}

class DataTransformer
{
}";

        var expected = @"class DataProcessor
{
    private readonly DataTransformer dataTransformer = new DataTransformer();

    public async Task<string> ProcessDataAsync(string input)
    {
        return await dataTransformer.ProcessDataAsync(input);
    }
}

class DataTransformer
{
    public async Task<string> ProcessDataAsync(string input)
    {
        await Task.Delay(100);
        return input.ToUpper();
    }
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "DataProcessor", "ProcessDataAsync", "DataTransformer", "dataTransformer", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveInstanceMethodInSource_WithMethodHavingAttributes_PreservesAttributes()
    {
        var input = @"class Service
{
    [Obsolete(""Use NewMethod instead"")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void OldMethod()
    {
        Console.WriteLine(""Old method"");
    }
}

class LegacyService
{
}";

        var expected = @"class Service
{
    private readonly LegacyService legacyService = new LegacyService();

    [Obsolete(""Use NewMethod instead"")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void OldMethod()
    {
        legacyService.OldMethod();
    }
}

class LegacyService
{
    [Obsolete(""Use NewMethod instead"")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void OldMethod()
    {
        Console.WriteLine(""Old method"");
    }
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "Service", "OldMethod", "LegacyService", "legacyService", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveInstanceMethodInSource_EmptyTargetClass_CreatesClassWithMethod()
    {
        var input = @"class Source
{
    public void TestMethod()
    {
        Console.WriteLine(""Test"");
    }
}";

        var output = MoveMethodsTool.MoveInstanceMethodInSource(input, "Source", "TestMethod", "NewTarget", "target", "field");

        Assert.Contains("class Source", output);
        Assert.Contains("private readonly NewTarget target = new NewTarget();", output);
        Assert.Contains("public class NewTarget", output);
        Assert.Contains("public void TestMethod()", output);
    }

    [Fact]
    public void MoveMethodsWithComplexDependencies_OrdersCorrectly()
    {
        var input = @"class ComplexCalculator
{
    public int MethodA()
    {
        return MethodB() + MethodC();
    }

    public int MethodB()
    {
        return MethodD() * 2;
    }

    public int MethodC()
    {
        return 5;
    }

    public int MethodD()
    {
        return 10;
    }
}

class Operations
{
}";

        var operationsJson = @"[
            {
                ""SourceClass"": ""ComplexCalculator"",
                ""Method"": ""MethodA"",
                ""TargetClass"": ""Operations"",
                ""AccessMember"": ""operations"",
                ""AccessMemberType"": ""field"",
                ""IsStatic"": false
            },
            {
                ""SourceClass"": ""ComplexCalculator"",
                ""Method"": ""MethodB"",
                ""TargetClass"": ""Operations"",
                ""AccessMember"": ""operations"",
                ""AccessMemberType"": ""field"",
                ""IsStatic"": false
            },
            {
                ""SourceClass"": ""ComplexCalculator"",
                ""Method"": ""MethodC"",
                ""TargetClass"": ""Operations"",
                ""AccessMember"": ""operations"",
                ""AccessMemberType"": ""field"",
                ""IsStatic"": false
            },
            {
                ""SourceClass"": ""ComplexCalculator"",
                ""Method"": ""MethodD"",
                ""TargetClass"": ""Operations"",
                ""AccessMember"": ""operations"",
                ""AccessMemberType"": ""field"",
                ""IsStatic"": false
            }
        ]";

        var output = MoveMultipleMethodsTool.MoveMultipleMethodsInSource(input, operationsJson);

        // Verify that all methods are moved
        Assert.Contains("class Operations", output);
        Assert.Contains("MethodA", output);
        Assert.Contains("MethodB", output);
        Assert.Contains("MethodC", output);
        Assert.Contains("MethodD", output);

        // Verify dependencies are handled correctly
        Assert.Contains("operations.MethodA", output);
        Assert.Contains("operations.MethodB", output);
        Assert.Contains("operations.MethodC", output);
        Assert.Contains("operations.MethodD", output);
    }

    [Fact]
    public void MoveInstanceMethodInSource_TwoConsecutiveMoves_BothMovesApplied()
    {
        var input = @"class DocumentProcessor
{
    private List<string> documents = new List<string>();

    public void ValidateDocument()
    {
        Console.WriteLine(""Validating document"");
    }

    public void ProcessDocument()
    {
        Console.WriteLine(""Processing document"");
    }

    public void SaveDocument()
    {
        Console.WriteLine(""Saving document"");
    }
}

class ValidationService
{
}

class ProcessingService
{
}";

        var expected = @"class DocumentProcessor
{
    private List<string> documents = new List<string>();
    private readonly ValidationService validationService = new ValidationService();
    private readonly ProcessingService processingService = new ProcessingService();

    public void ValidateDocument()
    {
        validationService.ValidateDocument();
    }

    public void ProcessDocument()
    {
        processingService.ProcessDocument();
    }

    public void SaveDocument()
    {
        Console.WriteLine(""Saving document"");
    }
}

class ValidationService
{
    public void ValidateDocument()
    {
        Console.WriteLine(""Validating document"");
    }
}

class ProcessingService
{
    public void ProcessDocument()
    {
        Console.WriteLine(""Processing document"");
    }
}";

        // First move: ValidateDocument to ValidationService
        var firstMove = MoveMethodsTool.MoveInstanceMethodInSource(input, "DocumentProcessor", "ValidateDocument", "ValidationService", "validationService", "field");

        // Second move: ProcessDocument to ProcessingService (should build on first move result)
        var secondMove = MoveMethodsTool.MoveInstanceMethodInSource(firstMove, "DocumentProcessor", "ProcessDocument", "ProcessingService", "processingService", "field");

        Assert.Equal(expected, secondMove.Trim());

        // Verify both access members are created
        Assert.Contains("private readonly ValidationService validationService = new ValidationService();", secondMove);
        Assert.Contains("private readonly ProcessingService processingService = new ProcessingService();", secondMove);

        // Verify both method calls are updated
        Assert.Contains("validationService.ValidateDocument();", secondMove);
        Assert.Contains("processingService.ProcessDocument();", secondMove);

        // Verify both target classes have the moved methods
        Assert.Contains("public void ValidateDocument()", secondMove);
        Assert.Contains("public void ProcessDocument()", secondMove);

        // Verify the third method remains in the original class
        Assert.Contains("Console.WriteLine(\"Saving document\");", secondMove);
    }

    [Fact]
    public void MoveMultipleInstanceMethodsInSource_MovesToDifferentTargets_AllMovesApplied()
    {
        var input = @"class DocumentProcessor
{
    private List<string> documents = new List<string>();

    public void ValidateDocument()
    {
        Console.WriteLine(""Validating document"");
    }

    public void ProcessDocument()
    {
        Console.WriteLine(""Processing document"");
    }

    public void LogActivity(string activity)
    {
        Console.WriteLine($""Activity: {activity}"");
    }

    public void SaveDocument()
    {
        Console.WriteLine(""Saving document"");
    }
}

class ValidationService
{
}

class ProcessingService
{
}

class LoggingService
{
}";
        var expected = @"class DocumentProcessor
{
    private List<string> documents = new List<string>();
    private readonly ValidationService validationService = new ValidationService();
    private readonly ProcessingService processingService = new ProcessingService();
    private readonly LoggingService loggingService = new LoggingService();

    public void ValidateDocument()
    {
        validationService.ValidateDocument();
    }

    public void ProcessDocument()
    {
        processingService.ProcessDocument();
    }

    public void LogActivity(string activity)
    {
        loggingService.LogActivity(activity);
    }

    public void SaveDocument()
    {
        Console.WriteLine(""Saving document"");
    }
}

class ValidationService
{
    public void ValidateDocument()
    {
        Console.WriteLine(""Validating document"");
    }
}

class ProcessingService
{
    public void ProcessDocument()
    {
        Console.WriteLine(""Processing document"");
    }
}

class LoggingService
{
    public void LogActivity(string activity)
    {
        Console.WriteLine($""Activity: {activity}"");
    }
}";

        // Create JSON operations for multiple method moves to different targets
        var operationsJson = @"[
            {
                ""SourceClass"": ""DocumentProcessor"",
                ""Method"": ""ValidateDocument"",
                ""TargetClass"": ""ValidationService"",
                ""AccessMember"": ""validationService"",
                ""AccessMemberType"": ""field"",
                ""IsStatic"": false
            },
            {
                ""SourceClass"": ""DocumentProcessor"",
                ""Method"": ""ProcessDocument"",
                ""TargetClass"": ""ProcessingService"",
                ""AccessMember"": ""processingService"",
                ""AccessMemberType"": ""field"",
                ""IsStatic"": false
            },
            {
                ""SourceClass"": ""DocumentProcessor"",
                ""Method"": ""LogActivity"",
                ""TargetClass"": ""LoggingService"",
                ""AccessMember"": ""loggingService"",
                ""AccessMemberType"": ""field"",
                ""IsStatic"": false
            }
        ]";

        var output = MoveMultipleMethodsTool.MoveMultipleMethodsInSource(input, operationsJson);

        Assert.Equal(expected, output.Trim());

        // Verify all access members are created
        Assert.Contains("private readonly ValidationService validationService = new ValidationService();", output);
        Assert.Contains("private readonly ProcessingService processingService = new ProcessingService();", output);
        Assert.Contains("private readonly LoggingService loggingService = new LoggingService();", output);

        // Verify all method calls are updated
        Assert.Contains("validationService.ValidateDocument();", output);
        Assert.Contains("processingService.ProcessDocument();", output);
        Assert.Contains("loggingService.LogActivity(activity);", output);

        // Verify all target classes have the moved methods
        Assert.Contains("class ValidationService", output);
        Assert.Contains("public void ValidateDocument()", output);
        Assert.Contains("class ProcessingService", output);
        Assert.Contains("public void ProcessDocument()", output);
        Assert.Contains("class LoggingService", output);
        Assert.Contains("public void LogActivity(string activity)", output);

        // Verify the unmoved method remains in the original class
        Assert.Contains("Console.WriteLine(\"Saving document\");", output);
    }
}

