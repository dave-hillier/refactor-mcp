namespace Shop
{
    public class Employee
    {
    }

    public class Manager : Employee
    {
        public string Title() => "Manager";
    }

    public class Engineer : Employee
    {
        public string Title() => "Engineer";
    }
}
