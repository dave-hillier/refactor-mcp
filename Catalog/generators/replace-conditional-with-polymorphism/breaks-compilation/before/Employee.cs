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
        _ => 0,
    };
}

public sealed class Engineer : Employee
{
    public override EmployeeType Type => EmployeeType.Engineer;

    public new int Bonus() => 1;
}

public sealed class Salesman : Employee
{
    public override EmployeeType Type => EmployeeType.Salesman;
}
