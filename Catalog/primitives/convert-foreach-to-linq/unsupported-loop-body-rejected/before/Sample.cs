public class Sample
{
    public int Product(int[] values)
    {
        var product = 1;
        /*^*/foreach (var value in values)
        {
            product *= value;
        }

        return product;
    }
}
