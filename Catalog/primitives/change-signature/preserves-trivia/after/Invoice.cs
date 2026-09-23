namespace Shop;

public class Invoice
{
    /// <summary>Builds a line.</summary>
    public string Line(
        int quantity,
        string product, // what was sold
        decimal price)
    {
        return product + quantity + price;
    }

    public string Print()
    {
        // two line items
        return Line(
            2,
            "pen", // product
            1.5m) + Line(/* count */ 1, "cup", 2m);
    }
}
