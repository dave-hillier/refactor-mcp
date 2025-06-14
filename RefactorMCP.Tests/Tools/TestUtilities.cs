using System;
using System.IO;
using System.Threading.Tasks;

namespace RefactorMCP.Tests;

public static class TestUtilities
{
    public static string GetSolutionPath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            var solutionFile = Path.Combine(dir.FullName, "RefactorMCP.sln");
            if (File.Exists(solutionFile))
                return solutionFile;
            dir = dir.Parent;
        }
        return "./RefactorMCP.sln";
    }

    public static async Task CreateTestFile(string filePath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, content);
    }

    public static string GetSampleCodeForExtractMethod() => """
using System;
public class TestClass
{
    public int Calculate(int a, int b)
    {
        if (a < 0 || b < 0)
        {
            throw new ArgumentException(\"Negative numbers not allowed\");
        }

        var result = a + b;
        return result;
    }
}
""";

    public static string GetSampleCodeForIntroduceField() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetSolutionPath())!, "RefactorMCP.Tests", "ExampleCode.cs"));

    public static string GetSampleCodeForIntroduceVariable() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetSolutionPath())!, "RefactorMCP.Tests", "ExampleCode.cs"));

    public static string GetSampleCodeForMakeFieldReadonly() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetSolutionPath())!, "RefactorMCP.Tests", "ExampleCode.cs"));

    public static string GetSampleCodeForMakeFieldReadonlyNoInit() => """
using System;
public class TestClass
{
    private string description;
}
""";

    public static string GetSampleCodeForTransformSetter() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetSolutionPath())!, "RefactorMCP.Tests", "ExampleCode.cs"));

    public static string GetSampleCodeForConvertToStaticInstance() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetSolutionPath())!, "RefactorMCP.Tests", "ExampleCode.cs"));

    public static string GetSampleCodeForMoveStaticMethod() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetSolutionPath())!, "RefactorMCP.Tests", "ExampleCode.cs"));

    public static string GetSampleCodeForMoveStaticMethodWithUsings() => """
using System;
using System.Collections.Generic;

public class TestClass
{
    public static void PrintList(List<int> numbers)
    {
        Console.WriteLine(string.Join(",", numbers));
    }
}

public class UtilClass { }
""";

    public static string GetSampleCodeForMoveInstanceMethod() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetSolutionPath())!, "RefactorMCP.Tests", "ExampleCode.cs"));

    public static string GetSampleCodeForConvertToExtension() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetSolutionPath())!, "RefactorMCP.Tests", "ExampleCode.cs"));

    public static string GetSampleCodeForSafeDelete() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetSolutionPath())!, "RefactorMCP.Tests", "ExampleCode.cs"));

    public static string GetSampleCodeForMoveInstanceMethodWithDependencies() => """
using System;
using System.Collections.Generic;

namespace Test.Domain
{
    public class OrderProcessor
    {
        private readonly string processorId;
        private List<string> log = new();

        public OrderProcessor(string id)
        {
            processorId = id;
        }

        public bool ValidateOrder(decimal amount)
        {
            return amount > 0;
        }

        // This method should be moved to PaymentService
        private bool ProcessPayment(decimal amount, string cardNumber)
        {
            if (string.IsNullOrEmpty(cardNumber))
                return false;

            log.Add($"Processing payment of {amount} for processor {processorId}");

            // Simulate payment processing
            return amount <= 1000;
        }

        public void CompleteOrder(decimal amount, string cardNumber)
        {
            if (ValidateOrder(amount) && ProcessPayment(amount, cardNumber))
            {
                log.Add("Order completed successfully");
            }
        }
    }

    public class PaymentService
    {
        // Target class for the moved method
    }
}
""";
    public static string GetSampleCodeForInlineMethod() => """
using System;

public class InlineSample
{
    private void Helper()
    {
        Console.WriteLine("Hi");
    }

    public void Call()
    {
        Helper();
        Console.WriteLine("Done");
    }
}
""";

    public static string GetSampleCodeForCleanupUsings() => """
using System;
using System.Text;

public class CleanupSample
{
    public void Say() => Console.WriteLine("Hi");
}
""";

    public static string GetSampleCodeForMoveClassToFile() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetSolutionPath())!, "RefactorMCP.Tests", "ExampleCode.cs"));

    public static string GetSampleCodeForRenameSymbol() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetSolutionPath())!, "RefactorMCP.Tests", "ExampleCode.cs"));
}

