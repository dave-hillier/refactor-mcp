namespace Staff;

public class Employee
{
    public const int Engineer = 0;
    public const int Salesman = 1;

    private readonly int _type;

    public Employee(int type)
    {
        _type = type;
    }

    public bool Sells() => _type == Salesman;
}
