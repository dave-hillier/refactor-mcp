namespace Staff;

public enum EmployeeType
{
    Engineer,
    Manager
}

public class Employee
{
    private EmployeeType _type;

    public Employee(EmployeeType type)
    {
        _type = type;
    }

    public void Promote()
    {
        _type = EmployeeType.Manager;
    }

    public bool Manages() => _type == EmployeeType.Manager;
}
