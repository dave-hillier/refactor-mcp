namespace Shop
{
    public class Employee
    {
        public string Name() => "employee";
    }

    public class Manager : Employee
    {
        public new string Name;
    }
}
