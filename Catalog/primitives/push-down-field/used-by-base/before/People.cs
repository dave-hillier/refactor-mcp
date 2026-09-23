namespace Shop
{
    public class Employee
    {
        protected int Quota;

        public bool HasQuota() => Quota > 0;
    }

    public class Salesman : Employee
    {
        public int Target() => Quota;
    }
}
