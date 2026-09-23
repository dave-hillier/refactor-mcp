using System;

namespace Staff;

public enum EmployeeType
{
    Engineer,
    Salesman
}

/// <summary>Someone on the payroll.</summary>
public abstract class Employee
{
    /// <summary>What the employee does.</summary>
    protected abstract EmployeeType Type { get; }

    /// <summary>Hires someone.</summary>
    protected Employee(string name)
    {
        // The name is required.
        Name = name;
    }

    public static Employee Create(EmployeeType type, string name) => type switch
    {
        EmployeeType.Engineer => new Engineer(name),
        EmployeeType.Salesman => new Salesman(name),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public string Name { get; }

    public bool Sells() => Type == EmployeeType.Salesman;
}

public sealed class Engineer : Employee
{
    public Engineer(string name) : base(name)
    {
    }

    protected override EmployeeType Type => EmployeeType.Engineer;
}

public sealed class Salesman : Employee
{
    public Salesman(string name) : base(name)
    {
    }

    protected override EmployeeType Type => EmployeeType.Salesman;
}
