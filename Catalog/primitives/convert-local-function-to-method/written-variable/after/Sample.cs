public class Sample
{
    public int Total(int[] prices)
    {
        var sum = 0;
        foreach (var price in prices)
            Add(price, ref sum);
        return sum;
    }

    private static void Add(int price, ref int sum)
    {
        sum += price;
    }
}
