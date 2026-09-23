namespace Shop
{
    public class Employee
    {
        public decimal Salary;
    }

    public class Manager : Employee
    {
        public int Grade;

        public decimal Bonus() => Salary * Grade / 10;
    }
}
