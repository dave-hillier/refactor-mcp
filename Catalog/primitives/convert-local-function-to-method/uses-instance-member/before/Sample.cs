public class Sample
{
    private int _rate = 20;

    public int Total(int[] prices)
    {
        var sum = 0;
        foreach (var price in prices)
            sum += WithTax(price);
        return sum;

        int /*^*/WithTax(int price) => price * (100 + _rate) / 100;
    }
}
