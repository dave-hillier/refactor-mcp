namespace Staff;

public sealed class Salesman : Employee
{
    public Salesman(int salary) : base(salary)
    {
    }

    public override EmployeeType Type => EmployeeType.Salesman;
}
