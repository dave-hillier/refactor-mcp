namespace Staff
{
    public class Payroll
    {
        public decimal Pay(Employee employee, decimal sales) =>
            employee is Salesman salesman ? 1000m + salesman.Bonus(sales) : 1000m;
    }
}
