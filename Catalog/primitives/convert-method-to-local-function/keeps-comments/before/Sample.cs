public class Sample
{
    public int Total(int[] prices)
    {
        var sum = 0;
        foreach (var price in prices)
            sum += WithTax(price);

        // Rounded down.
        return sum;
    }

    // Twenty percent, in whole units.
    private static int WithTax(int price)
    {
        // Multiply first to keep precision.
        return price * 120 / 100;
    }

    public int Count(int[] prices) => prices.Length;
}
