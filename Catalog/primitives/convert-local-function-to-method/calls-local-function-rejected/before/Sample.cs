public class Sample
{
    public int Total(int[] prices)
    {
        var sum = 0;
        foreach (var price in prices)
            sum += WithTax(price);
        return sum;

        int /*^*/WithTax(int price) => Round(price * 120) / 100;

        int Round(int value) => value - value % 10;
    }
}
