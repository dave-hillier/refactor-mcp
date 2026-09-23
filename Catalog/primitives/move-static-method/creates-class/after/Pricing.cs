namespace Shop;

public static class Pricing
{
    public static decimal Vat(decimal net) => net * 0.2m;
}
