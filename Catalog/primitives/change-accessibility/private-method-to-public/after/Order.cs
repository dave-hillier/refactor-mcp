namespace Shop;

public class Order
{
    public decimal Total()
    {
        return 100m + Tax();
    }

    public decimal Tax()
    {
        return 20m;
    }
}
