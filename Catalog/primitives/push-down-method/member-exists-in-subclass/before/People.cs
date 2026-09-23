namespace Shop
{
    public class Employee
    {
        public string Describe() => "employee";
    }

    public class Salesman : Employee
    {
        public new string Describe() => "salesman";
    }
}
