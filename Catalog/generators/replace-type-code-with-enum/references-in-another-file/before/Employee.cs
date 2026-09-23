namespace Staff;

public class Employee
{
    public const int Engineer = 0;
    public const int Manager = 1;

    public Employee(int type, string name)
    {
        Type = type;
        Name = name;
    }

    public int Type { get; }

    public string Name { get; }
}
