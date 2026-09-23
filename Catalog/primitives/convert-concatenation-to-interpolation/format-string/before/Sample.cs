public class Sample
{
    public string Describe(string name, int count, bool ok, decimal price, int a, int b)
    {
        return /*^*/"Price: " + price.ToString("F2") + " each";
    }
}
