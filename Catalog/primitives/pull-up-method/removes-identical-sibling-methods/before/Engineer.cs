namespace Shop
{
    public class Engineer : Employee
    {
        public decimal AnnualCost()
        {
            return MonthlyCost * 12;
        }
    }
}
