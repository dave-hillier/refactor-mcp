namespace Shop
{
    /// <summary>An amount in a currency.</summary>
    public record Money
    {
        // Rounding applies to every amount.
        private readonly int _scale;

        public Money(decimal amount, string currency, int scale)
        {
            Amount = amount;
            Currency = currency;
            _scale = scale;
        }

        public decimal Amount { get; }

        public string Currency { get; }

        public decimal Rounded => decimal.Round(Amount, _scale);
    }
}
