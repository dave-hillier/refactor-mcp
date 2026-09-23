public class Sample
{
    public int Total(int[] prices, int rate)
    {
        var bonus = prices.Length > 3 ? 1 : 0;
        var sum = 0;
        foreach (var price in prices)
            sum += /*^*/WithTax(price);
        return sum;

        int WithTax(int price)
        {
            return price * (100 + rate) / 100 + bonus;
        }
    }
}
