namespace Staff
{
    public class Payroll
    {
        public decimal Pay(Employee salesman, decimal sales) => 1000m + salesman.Bonus(sales);
    }
}
