namespace Staff;

public enum EmployeeType
{
    Engineer,
    Salesman
}

/// <summary>Someone on the payroll.</summary>
public class Employee
{
    /// <summary>What the employee does.</summary>
    private readonly EmployeeType _type;

    /// <summary>Hires someone.</summary>
    public Employee(EmployeeType type, string name)
    {
        // The name is required.
        Name = name;
        _type = type; // fixed for life
    }

    public string Name { get; }

    public bool Sells() => _type == EmployeeType.Salesman;
}
