namespace Shop;

public partial class Order
{
    public decimal Price()
    {
        return _total - Discount(0.1m);

        decimal Discount(decimal rate) => _total * rate;
    }
}
