namespace Shop
{
    public class Employee
    {
        public decimal Salary;

        protected decimal Rate() => 0.1m;

        public decimal Bonus() => Salary * Rate();
    }

    public class Salesman : Employee
    {
        public decimal Commission(decimal sales) => sales * Rate();
    }
}
