#nullable enable

// Money values used across the shop.
namespace Shop
{
    public class Money
    {
        public Money(decimal amount)
        {
            Amount = amount;
        }

        public decimal Amount { get; }

        public override bool Equals(object? obj) => obj is Money other && other.Amount == Amount;

        public override int GetHashCode() => Amount.GetHashCode();
    }
}
