namespace Staff;

public class Employee
{
    private readonly EmployeeType _type;

    public Employee(EmployeeType type)
    {
        _type = type;
    }

    public EmployeeType Type => _type;

    public int Bonus()
    {
        switch (_type)
        {
            case EmployeeType.Engineer:
                return 100;
            case EmployeeType.Salesman:
                return 200;
            default:
                return 300;
        }
    }

    public bool IsManager() => _type == EmployeeType.Manager;
}
