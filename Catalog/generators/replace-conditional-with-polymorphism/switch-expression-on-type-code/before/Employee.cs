using System;

namespace Staff;

public enum EmployeeType
{
    Engineer,
    Salesman
}

public abstract class Employee
{
    public abstract EmployeeType Type { get; }

    public int Bonus() => Type switch
    {
        EmployeeType.Engineer => 100,
        EmployeeType.Salesman => 200,
        _ => throw new ArgumentOutOfRangeException(),
    };
}

public sealed class Engineer : Employee
{
    public override EmployeeType Type => EmployeeType.Engineer;
}

public sealed class Salesman : Employee
{
    public override EmployeeType Type => EmployeeType.Salesman;
}
