namespace Shop;

public class Order
{
    public Order(string customer)
    {
        Customer = customer;
    }

    public string Customer { get; }

    public string Note { get; set; } = "";

    public static Order Gift(string customer) => new Order(customer) { Note = "gift" };
}
