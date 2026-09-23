namespace Shop;

public interface IShipping
{
    decimal Cost(decimal weight);
}

public class Courier : IShipping
{
    public decimal Cost(decimal weight)
    {
        return weight * 2m;
    }
}
