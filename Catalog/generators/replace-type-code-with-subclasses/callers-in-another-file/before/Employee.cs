namespace Staff;

public class Employee
{
    private readonly EmployeeType _type;

    public Employee(EmployeeType type, string name)
    {
        _type = type;
        Name = name;
    }

    public string Name { get; }

    public int Bonus()
    {
        switch (_type)
        {
            case EmployeeType.Engineer:
                return 100;
            default:
                return 200;
        }
    }
}
