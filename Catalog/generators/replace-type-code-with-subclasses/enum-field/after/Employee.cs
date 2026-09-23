using System;

namespace Staff;

public abstract class Employee
{
    protected abstract EmployeeType Type { get; }

    protected Employee(string name)
    {
        Name = name;
    }

    public static Employee Create(EmployeeType type, string name) => type switch
    {
        EmployeeType.Engineer => new Engineer(name),
        EmployeeType.Salesman => new Salesman(name),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public string Name { get; }

    public int Bonus()
    {
        switch (Type)
        {
            case EmployeeType.Engineer:
                return 100;
            default:
                return 200;
        }
    }
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
