using System;
using System.Collections.Generic;
using System.Linq;

namespace RefactorMCP.Tests.Examples
{
    // Example class for demonstrating refactoring tools
    public class Calculator
    {
        private List<int> numbers = new List<int>();
        private readonly string operatorSymbol;

        public Calculator(string op)
        {
            operatorSymbol = op;
        }

        // Example for Extract Method refactoring
        public int Calculate(int a, int b)
        {
            // This code block can be extracted into a method
            if (a < 0 || b < 0)
            {
                throw new ArgumentException("Negative numbers not allowed");
            }

            var result = a + b;
            numbers.Add(result);
            Console.WriteLine($"Result: {result}");
            return result;
        }

        // Example for Introduce Field refactoring
        public double GetAverage()
        {
            return numbers.Sum() / (double)numbers.Count; // This expression can become a field
        }

        // Example for Introduce Variable refactoring
        public string FormatResult(int value)
        {
            return $"The calculation result is: {value * 2 + 10}"; // Complex expression can become a variable
        }

        // Example for Convert to Static refactoring
        public string GetFormattedNumber(int number)
        {
            return $"{operatorSymbol}: {number}"; // Uses instance field, can be converted to static
        }

        // Example for Make Field Readonly refactoring
        private string format = "Currency"; // This field can be made readonly

        public void SetFormat(string newFormat)
        {
            format = newFormat; // This assignment would move to constructor
        }

        // Example for property with setter that can be converted to init
        public string Name { get; set; } = "Default Calculator";

        // Example for Move Method refactoring
        public static string FormatCurrency(decimal amount)
        {
            return $"${amount:F2}"; // This static method could be moved to a utility class
        }

        // Example instance method that could be moved
        public void LogOperation(string operation)
        {
            Console.WriteLine($"[{DateTime.Now}] {operation}");
        }

        // Example for Safe Delete - unused parameter
        public int Multiply(int x, int y, int unusedParam)
        {
            // This parameter is unused and can be safely deleted
            return x * y;
        }

        // Example for Safe Delete - unused method and variable
        private void UnusedHelper()
        {
#pragma warning disable CS0219 // Variable is intentionally unused for testing
            int tempValue = 0; // tempValue can be safely deleted
#pragma warning restore CS0219
        }

        // Example field that might be safe to delete
        // Field intentionally unused for safe-delete tests
#pragma warning disable CS0414 // Field is used for analyzer tests
        private int deprecatedCounter = 0;
#pragma warning restore CS0414
    }

    // Example class for Move Method target
    public class MathUtilities
    {
        // Target location for moved static methods
    }

    // Example class for Move Instance Method
    public class Logger
    {
        // Target location for moved instance methods
        public void Log(string message)
        {
            Console.WriteLine($"[LOG] {message}");
        }
    }
}
