namespace Shop
{
    public class Employee
    {
        public virtual decimal Rate() => 0.1m;
    }

    public class Salesman : Employee
    {
    }

    public class Manager : Employee
    {
        public override decimal Rate() => 0.2m;
    }
}
