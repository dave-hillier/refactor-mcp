namespace Shop
{
    public class Employee
    {
    }

    public sealed class Salesman : Employee
    {
        public decimal Commission(decimal sales) => sales * Rate();

        private decimal Rate() => 0.1m;
    }
}
