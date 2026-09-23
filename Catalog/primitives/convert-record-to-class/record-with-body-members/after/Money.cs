using System;
using System.Collections.Generic;

namespace Shop
{
    /// <summary>An amount in a currency.</summary>
    public class Money : IEquatable<Money>
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

        public virtual bool Equals(Money other)
        {
            return other is not null
                && GetType() == other.GetType()
                && EqualityComparer<int>.Default.Equals(_scale, other._scale)
                && EqualityComparer<decimal>.Default.Equals(Amount, other.Amount)
                && EqualityComparer<string>.Default.Equals(Currency, other.Currency);
        }

        public override bool Equals(object obj) => Equals(obj as Money);

        public override int GetHashCode() => HashCode.Combine(_scale, Amount, Currency);

        public override string ToString() => $"Money {{ Amount = {Amount}, Currency = {Currency}, Rounded = {Rounded} }}";

        public static bool operator ==(Money left, Money right) => left is null ? right is null : left.Equals(right);

        public static bool operator !=(Money left, Money right) => !(left == right);
    }
}
