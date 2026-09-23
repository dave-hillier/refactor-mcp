namespace Payroll
{
    public class Review
    {
        public void Apply(Employee employee, bool outstanding)
        {
            if (outstanding)
                employee.TenPercentRaise();
            else
                employee.FivePercentRaise();
        }
    }
}
