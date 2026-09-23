namespace Shop
{
    public class Salesman : Employee
    {
        public decimal Commission;

        public decimal AnnualCost()
        {
            return MonthlyCost * 12;
        }
    }
}
