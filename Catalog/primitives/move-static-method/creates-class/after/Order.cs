namespace Shop;

public class Order
{
    public static decimal Vat(decimal net) => Pricing.Vat(net);
}
