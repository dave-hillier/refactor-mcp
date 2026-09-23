namespace Shop;

public class Order
{
    public string Summary(int quantity, decimal unitPrice)
    {
        return "Total: " + /*[*/quantity * unitPrice/*]*/;
    }
}
