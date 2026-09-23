namespace Staff;

/// <summary>Someone on the payroll.</summary>
public class Employee
{
    // The kinds of employee.
    /// <summary>Writes the software.</summary>
    public const int Engineer = 0;

    /// <summary>Sells the software.</summary>
    public const int Salesman = 1;

    private int _type; // set once

    public Employee(int type) => _type = type;

    /* commission applies */
    public bool Sells() => _type == Salesman; // not engineers
}
