public class Sample
{
    public int Total(int[] prices)
    {
        var sum = 0;
        foreach (var price in prices)
            sum += ApplyTax(price);
        return sum;
    }

    private static int ApplyTax(int price) => price * 120 / 100;
}
