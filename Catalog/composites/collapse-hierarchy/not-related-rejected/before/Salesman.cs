namespace Staff
{
    public class Salesman : Employee
    {
        protected decimal _commission = 0.1m;

        public decimal Bonus(decimal sales) => sales * _commission;
    }
}
