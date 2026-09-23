namespace Shop;

public class Order
{
    public Order(string customer, int quantity)
    {
        Customer = customer;
        Quantity = quantity;
    }

    public string Customer { get; }

    public int Quantity { get; }
}
