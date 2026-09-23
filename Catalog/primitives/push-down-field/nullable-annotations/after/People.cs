namespace Shop
{
    public class Employee
    {
    }

    public class Salesman : Employee
    {
        public string? Territory;

        public string Region() => Territory ?? "none";
    }
}
