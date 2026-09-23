namespace Shop;

public class Report
{
    public string Summary(string customer, int quantity)
    {
        var line = /*^*/new { Customer = customer, Quantity = quantity };
        return line.Customer + ": " + line.Quantity;
    }
}
