namespace Shop
{
    public class Employee
    {
        public decimal Salary;
    }

    public class Manager : Employee
    {
        public decimal Net() => Salary - Tax(Salary);

        private decimal Tax(decimal amount) => amount * 0.2m;
    }
}
