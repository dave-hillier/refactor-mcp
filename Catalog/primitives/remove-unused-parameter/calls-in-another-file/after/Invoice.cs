namespace Shop;

public class Invoice
{
    public string Line(string product, decimal price)
    {
        return product + " " + price;
    }
}
