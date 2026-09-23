namespace Staff;

public class Employee
{
    public Employee(EmployeeType type, string name)
    {
        Type = type;
        Name = name;
    }

    public EmployeeType Type { get; }

    public string Name { get; }
}
