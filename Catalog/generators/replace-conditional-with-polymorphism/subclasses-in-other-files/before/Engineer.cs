namespace Staff;

public sealed class Engineer : Employee
{
    public Engineer(int salary) : base(salary)
    {
    }

    public override EmployeeType Type => EmployeeType.Engineer;
}
