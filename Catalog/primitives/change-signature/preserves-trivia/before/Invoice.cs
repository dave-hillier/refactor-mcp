namespace Shop;

public class Invoice
{
    /// <summary>Builds a line.</summary>
    public string Line(
        string product, // what was sold
        int quantity,
        decimal price)
    {
        return product + quantity + price;
    }

    public string Print()
    {
        // two line items
        return Line(
            "pen", // product
            2,
            1.5m) + Line("cup", /* count */ 1, 2m);
    }
}
