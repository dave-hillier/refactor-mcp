namespace Shop;

public class Checkout
{
    public Order Single(string customer) => Order.Create(customer, 1);

    public Order Bulk(string customer)
    {
        // Bulk orders are always a dozen.
        return Order.Create(quantity: 12, customer: customer);
    }
}
