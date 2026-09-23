namespace Staff;

public enum EmployeeType
{
    Engineer,
    Salesman,
    Manager
}

public abstract class Employee
{
    protected readonly int _salary;

    protected Employee(int salary)
    {
        _salary = salary;
    }

    public abstract EmployeeType Type { get; }

    /// <summary>The bonus for the year.</summary>
    public virtual int Bonus() => 0;
}
