namespace Staff
{
    public class Employee
    {
        public string Name;
        protected decimal _commission = 0.1m;

        public decimal Bonus(decimal sales) => sales * _commission;
    }
}
