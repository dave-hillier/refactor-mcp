namespace Shop;

public class Order
{
    public Order(string customer)
    {
        Customer = customer;
    }

    public string Customer { get; }
}
