public class Sample
{
    public int Total(int[] prices)
    {
        var /*^*/sum = 0;
        foreach (var price in prices)
            sum += price;
        return sum;
    }
}
