namespace Staff
{
    public class Employee
    {
        public string Name { get; set; }

        // Paid on every sale.
        public decimal Rate { get; set; } = 0.1m;

        /// <summary>The commission earned on <paramref name="sales"/>.</summary>
        public decimal Commission(decimal sales) => sales * Rate;
    }
}
