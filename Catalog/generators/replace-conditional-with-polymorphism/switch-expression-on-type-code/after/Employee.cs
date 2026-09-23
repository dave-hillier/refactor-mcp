namespace Staff;

public enum EmployeeType
{
    Engineer,
    Salesman
}

public abstract class Employee
{
    public abstract EmployeeType Type { get; }

    public abstract int Bonus();
}

public sealed class Engineer : Employee
{
    public override EmployeeType Type => EmployeeType.Engineer;

    public override int Bonus() => 100;
}

public sealed class Salesman : Employee
{
    public override EmployeeType Type => EmployeeType.Salesman;

    public override int Bonus() => 200;
}
