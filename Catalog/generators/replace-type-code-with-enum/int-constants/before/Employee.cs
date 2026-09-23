namespace Staff;

public class Employee
{
    public const int Engineer = 0;
    public const int Salesman = 1;
    public const int Manager = 2;

    private readonly int _type;

    public Employee(int type)
    {
        _type = type;
    }

    public int Type => _type;

    public int Bonus()
    {
        switch (_type)
        {
            case Engineer:
                return 100;
            case Salesman:
                return 200;
            default:
                return 300;
        }
    }

    public bool IsManager() => _type == Manager;
}
