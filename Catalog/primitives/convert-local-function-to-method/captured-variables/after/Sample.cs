public class Sample
{
    public int Total(int[] prices, int rate)
    {
        var bonus = prices.Length > 3 ? 1 : 0;
        var sum = 0;
        foreach (var price in prices)
            sum += WithTax(price, rate, bonus);
        return sum;
    }

    private static int WithTax(int price, int rate, int bonus)
    {
        return price * (100 + rate) / 100 + bonus;
    }
}
