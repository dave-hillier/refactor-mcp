namespace Staff;

public enum EmployeeType
{
    Engineer,
    Salesman,
    Manager
}

public abstract class Employee
{
    private readonly int _salary;

    protected Employee(int salary)
    {
        _salary = salary;
    }

    public abstract EmployeeType Type { get; }

    /// <summary>The bonus for the year.</summary>
    public int Bonus()
    {
        switch (Type)
        {
            case EmployeeType.Engineer:
                return 100;
            case EmployeeType.Manager:
                // Managers share in the salary pool.
                var share = _salary / 10;
                return share + 50;
            default:
                return 0;
        }
    }
}
