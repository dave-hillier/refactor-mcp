public class Sample
{
    public string Describe(string name, int count, bool ok, decimal price, int a, int b)
    {
        return $"Status: {(ok ? "on" : "off")}";
    }
}
