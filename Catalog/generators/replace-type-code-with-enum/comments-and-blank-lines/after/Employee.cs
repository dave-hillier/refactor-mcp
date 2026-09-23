namespace Staff;

/// <summary>Someone on the payroll.</summary>
public class Employee
{
    private EmployeeType _type; // set once

    public Employee(EmployeeType type) => _type = type;

    /* commission applies */
    public bool Sells() => _type == EmployeeType.Salesman; // not engineers
}
