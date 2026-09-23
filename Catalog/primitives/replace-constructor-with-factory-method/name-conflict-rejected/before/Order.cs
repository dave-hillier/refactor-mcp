namespace Shop;

public class Order
{
    public Order(string customer)
    {
        Customer = customer;
    }

    public string Customer { get; }

    public static Order Create(string customer) => new Order(customer.Trim());
}
