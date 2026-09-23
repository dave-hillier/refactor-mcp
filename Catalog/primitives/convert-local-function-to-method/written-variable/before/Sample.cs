public class Sample
{
    public int Total(int[] prices)
    {
        var sum = 0;
        foreach (var price in prices)
            Add(price);
        return sum;

        void /*^*/Add(int price)
        {
            sum += price;
        }
    }
}
