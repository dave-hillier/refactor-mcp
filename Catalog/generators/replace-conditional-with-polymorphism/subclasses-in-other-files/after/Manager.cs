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

    public override int Bonus()
    {
        // Managers share in the salary pool.
        var share = _salary / 10;
        return share + 50;
    }
}
