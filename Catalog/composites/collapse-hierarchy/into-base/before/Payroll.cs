namespace Staff
{
    public class Payroll
    {
        public decimal Pay(Salesman salesman, decimal sales) => 1000m + salesman.Bonus(sales);
    }
}
