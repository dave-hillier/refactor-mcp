namespace Shop
{
    public class Employee
    {
        protected virtual decimal Rate() => 0.1m;
    }

    public sealed class Salesman : Employee
    {
        public decimal Commission(decimal sales) => sales * Rate();
    }
}
