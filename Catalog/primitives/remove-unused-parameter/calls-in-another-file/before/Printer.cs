namespace Shop;

public class Printer
{
    public string Print(Invoice invoice)
    {
        return invoice.Line(1, "pen", 1.5m) + invoice.Line(price: 2m, index: 2, product: "ink");
    }
}
