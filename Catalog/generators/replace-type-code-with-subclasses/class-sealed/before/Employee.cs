namespace Staff;

public enum EmployeeType
{
    Engineer,
    Salesman
}

public sealed class Employee
{
    private readonly EmployeeType _type;

    public Employee(EmployeeType type)
    {
        _type = type;
    }

    public bool Sells() => _type == EmployeeType.Salesman;
}
