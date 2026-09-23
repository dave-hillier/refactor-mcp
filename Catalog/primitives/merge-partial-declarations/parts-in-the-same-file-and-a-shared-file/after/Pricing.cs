namespace Shop;

public static class Pricing
{
    public static decimal Discount(Order order) => order.Total() * 0.1m;
}
