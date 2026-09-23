public class Sample
{
    public int Total(int price)
    {
        return WithTax(price);
    }

    public int Double(int price)
    {
        return WithTax(price) * 2;
    }

    private static int WithTax(int price) => price * 120 / 100;
}
