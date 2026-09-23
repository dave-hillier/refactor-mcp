namespace Shop;

public partial class Order
{
    public decimal Total() => Quantity * Price;
}

public static class Pricing
{
    public static decimal Discount(Order order) => order.Total() * 0.1m;
}
