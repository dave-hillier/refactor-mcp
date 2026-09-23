namespace Shop;

public class Tax
{
    private readonly decimal _rate = 0.2m;

    public decimal Vat(decimal price)
    {
        return price * /*[*/_rate/*]*/;
    }
}
