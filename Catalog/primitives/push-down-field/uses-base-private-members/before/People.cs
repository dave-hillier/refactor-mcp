namespace Shop
{
    public class Employee
    {
        private const int DefaultQuota = 10;
        protected int Quota = DefaultQuota;
    }

    public class Salesman : Employee
    {
        public int Target() => Quota;
    }
}
