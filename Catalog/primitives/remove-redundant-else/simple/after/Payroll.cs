namespace Staff
{
    public class Payroll
    {
        public decimal Pay(bool retired, decimal salary)
        {
            if (retired)
            {
                return 0m;
            }

            var bonus = salary * 0.1m;
            return salary + bonus;
        }
    }
}
