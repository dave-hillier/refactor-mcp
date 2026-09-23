namespace Shop;

internal class FlatPricing : IPricing
{
    public decimal Price(string sku) => 1m;
}
