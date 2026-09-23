namespace Shop
{
    public abstract class Employee
    {
        public decimal Salary;

        public abstract decimal Bonus();
    }

    public class Manager : Employee
    {
        private int _grade = 3;

        public override decimal Bonus() => Salary * _grade / 10;
    }

    public class Engineer : Employee
    {
        public override decimal Bonus()
        {
            return 100;
        }
    }
}
