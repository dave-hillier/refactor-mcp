namespace Shop
{
    public class Employee
    {
        public string? Territory;
    }

    public class Salesman : Employee
    {
        public string Region() => Territory ?? "none";
    }
}
