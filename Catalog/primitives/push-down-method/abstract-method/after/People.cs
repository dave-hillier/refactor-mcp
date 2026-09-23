namespace Shop
{
    public abstract class Employee
    {
        public decimal Salary;
    }

    public class Salesman : Employee
    {
        public decimal Sales;

        public decimal Commission() => Sales / 10;
    }

    public class Engineer : Employee
    {
        public decimal Commission()
        {
            return 0;
        }
    }
}
