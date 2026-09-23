namespace Shop;

public class Printer
{
    public string Print(Invoice invoice)
    {
        return invoice.Line("pen", 1.5m) + invoice.Line(product: "ink", price: 2m);
    }
}
