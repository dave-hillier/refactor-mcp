namespace Shop
{
    public class Employee
    {
        public int Quota;
    }

    public class Salesman : Employee
    {
        public new string Quota = "high";
    }
}
