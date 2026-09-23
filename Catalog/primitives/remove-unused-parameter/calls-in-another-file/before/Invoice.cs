namespace Shop;

public class Invoice
{
    public string Line(int index, string product, decimal price)
    {
        return product + " " + price;
    }
}
