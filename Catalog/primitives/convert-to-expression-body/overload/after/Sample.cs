public class Sample
{
    public string Format(string text)
    {
        return text.Trim();
    }

    public string Format(int number) => number.ToString();
}
