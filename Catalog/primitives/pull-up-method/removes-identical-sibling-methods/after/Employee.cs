namespace Shop
{
    public abstract class Employee
    {
        public decimal MonthlyCost;

        public decimal AnnualCost()
        {
            return MonthlyCost * 12;
        }
    }
}
