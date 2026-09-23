public class Employee
{
    private decimal _salary;

    public void Raise(decimal factor)
    {
        _salary *= factor;
    }

    public void Promote(string title)
    {
        Raise(1.2m);
        System.Console.WriteLine(title);
    }
}
