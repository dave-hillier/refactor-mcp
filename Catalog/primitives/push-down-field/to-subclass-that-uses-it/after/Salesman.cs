namespace Shop
{
    public class Salesman : Employee
    {
        public decimal Sales;
        public int Quota;

        public bool MetQuota() => Sales >= Quota;
    }
}
