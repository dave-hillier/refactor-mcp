namespace Shop
{
    public interface IDiscountPolicy
    {
        string Name { get; }

        decimal Rate(decimal total);

        int Priority();
    }
}
