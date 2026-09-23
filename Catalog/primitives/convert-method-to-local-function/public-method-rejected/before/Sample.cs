public class Sample
{
    public int Total(int price) => WithTax(price);

    public int WithTax(int price) => price * 120 / 100;
}
