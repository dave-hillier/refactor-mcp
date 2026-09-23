namespace Shop
{
    public abstract class Employee
    {
        public decimal Salary;

        public abstract decimal Commission();
    }

    public class Salesman : Employee
    {
        public decimal Sales;

        public override decimal Commission() => Sales / 10;
    }

    public class Engineer : Employee
    {
        public override decimal Commission()
        {
            return 0;
        }
    }
}
