namespace Staff
{
    public class Payroll
    {
        public decimal Pay(bool retired, decimal salary)
        {
            /*^*/if (retired)
            {
                var bonus = 100m;
                return bonus;
            }
            else
            {
                var bonus = salary * 0.1m;
                return salary + bonus;
            }
        }
    }
}
