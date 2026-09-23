namespace Shop;

public class Order
{
    private Order(string customer, int quantity)
    {
        Customer = customer;
        Quantity = quantity;
    }

    public static Order Create(string customer, int quantity) => new Order(customer, quantity);

    public string Customer { get; }

    public int Quantity { get; }
}
