namespace Shop
{
    public class Employee
    {
        private decimal _rate = 0.1m;

        public decimal Bonus(decimal salary) => salary * _rate;
    }

    public class Salesman : Employee
    {
        public decimal Paid(decimal salary) => salary + Bonus(salary);
    }
}
