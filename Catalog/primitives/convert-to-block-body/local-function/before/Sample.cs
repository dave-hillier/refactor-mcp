public class Sample
{
    public int Total(int[] prices)
    {
        var sum = 0;
        foreach (var price in prices)
            sum += WithTax(price);
        return sum;

        static int /*^*/WithTax(int price) => price * 120 / 100;
    }
}
