namespace Shop;

public class Shipping
{
    public decimal Cost(decimal weight)
    {
        var fee = 5m;
        return /*[*/fee + weight/*]*/;
    }
}
