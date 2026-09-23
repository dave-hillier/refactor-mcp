namespace Shop
{
    public class Salesman : Employee
    {
        public decimal Sales;

        public string QuotaReport()
        {
            return Name + " has a quota";
        }
    }
}
