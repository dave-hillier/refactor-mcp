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
        // a single line item
        return Line(
            "pen", // product
            2,
            1.5m);
    }
}
