namespace Shop;

public class Checkout
{
    public Invoice Bill(decimal amount) => new Invoice { Amount = amount };
}
