namespace Shop;

public class Line
{
}

public class Report
{
    public string Summary(string customer, int quantity)
    {
        var line = /*^*/new { Customer = customer, Quantity = quantity };
        return line.Customer + ": " + line.Quantity;
    }
}
