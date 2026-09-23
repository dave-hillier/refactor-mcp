namespace Shop
{
    public class Salesman : Employee
    {
        public decimal Sales;

        public bool MetQuota() => Sales >= Quota;
    }
}
