namespace Shop
{
    public class Employee
    {
        public decimal Salary;

        protected decimal Tax(decimal amount) => amount * 0.2m;
    }

    public class Manager : Employee
    {
        public decimal Net() => Salary - Tax(Salary);
    }
}
