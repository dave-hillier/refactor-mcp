namespace Shop;

public class Order
{
    public int Quantity { get; set; }

    public decimal Price { get; set; }

    public decimal Total() => Quantity * Price;
}
