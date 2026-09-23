namespace Staff;

public enum EmployeeType
{
    Engineer,
    Salesman
}

public class Employee
{
    private readonly EmployeeType _type;

    public Employee(EmployeeType type)
    {
        _type = type;
    }

    public Employee()
    {
        _type = EmployeeType.Engineer;
    }

    public bool Sells() => _type == EmployeeType.Salesman;
}
