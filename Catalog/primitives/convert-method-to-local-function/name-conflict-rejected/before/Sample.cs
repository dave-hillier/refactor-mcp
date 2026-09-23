public class Sample
{
    private int rate = 20;

    public int Total(int price)
    {
        var rate = 5;
        return WithTax(price) + rate;
    }

    private int WithTax(int price) => price * (100 + rate) / 100;
}
