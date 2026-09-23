namespace Shop;

public class Order
{
    private decimal _amount;

    public decimal Combined(Order other)
    {
        return _amount + other.Amount();
    }

    private decimal Amount() => _amount;
}
