namespace Shop
{
    public abstract class Employee
    {
    }

    public class Manager : Employee
    {
        public decimal Bonus() => 100;
    }

    public class Engineer : Employee
    {
    }
}
