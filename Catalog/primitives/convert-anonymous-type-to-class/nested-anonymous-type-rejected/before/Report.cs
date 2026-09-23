namespace Shop;

public class Report
{
    public object Summary(string customer, decimal net, decimal tax)
    {
        return /*^*/new { Customer = customer, Total = new { Net = net, Tax = tax } };
    }
}
