namespace Shop;

public interface IShipping
{
    decimal Cost(string region, decimal weight);
}

public class Courier : IShipping
{
    public decimal Cost(string region, decimal weight)
    {
        return weight * 2m;
    }
}

public class Post : IShipping
{
    decimal IShipping.Cost(string region, decimal weight)
    {
        return region == "EU" ? weight : weight * 3m;
    }
}
