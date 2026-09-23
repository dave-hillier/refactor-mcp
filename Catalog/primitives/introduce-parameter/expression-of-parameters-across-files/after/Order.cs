namespace Shop;

public class Order
{
    public string Summary(int quantity, decimal unitPrice, decimal total)
    {
        return "Total: " + total;
    }
}
