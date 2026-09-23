namespace Shop;

public interface IShipping
{
    decimal Cost(decimal weight, string region);
}

public class Courier : IShipping
{
    public decimal Cost(decimal weight, string region)
    {
        return weight * 2m;
    }
}

public class Post : IShipping
{
    decimal IShipping.Cost(decimal weight, string region)
    {
        return region == "EU" ? weight : weight * 3m;
    }
}
