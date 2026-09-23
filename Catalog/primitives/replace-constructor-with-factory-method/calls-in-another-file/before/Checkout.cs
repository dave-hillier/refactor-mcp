namespace Shop;

public class Checkout
{
    public Order Single(string customer) => new Order(customer, 1);

    public Order Bulk(string customer)
    {
        // Bulk orders are always a dozen.
        return new Order(quantity: 12, customer: customer);
    }
}
