namespace Shop;

public class Tax
{
    public decimal Vat(decimal price, decimal rate)
    {
        return price * rate;
    }

    public decimal Gross(decimal price)
    {
        return price + Vat(price, 0.2m);
    }
}
