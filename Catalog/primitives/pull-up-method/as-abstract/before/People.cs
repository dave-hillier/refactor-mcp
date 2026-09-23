namespace Shop
{
    public abstract class Employee
    {
        public decimal Salary;
    }

    public class Manager : Employee
    {
        private int _grade = 3;

        public decimal Bonus() => Salary * _grade / 10;
    }

    public class Engineer : Employee
    {
        public decimal Bonus()
        {
            return 100;
        }
    }
}
