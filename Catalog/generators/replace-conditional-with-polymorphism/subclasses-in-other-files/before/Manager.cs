namespace Staff;

public sealed class Manager : Employee
{
    public Manager(int salary) : base(salary)
    {
    }

    public override EmployeeType Type
    {
        get { return EmployeeType.Manager; }
    }
}
