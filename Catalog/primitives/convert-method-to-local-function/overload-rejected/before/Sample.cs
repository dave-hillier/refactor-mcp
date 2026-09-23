public class Sample
{
    public string Describe(int number, string text)
    {
        return Format(number) + Format(text);
    }

    private static string Format(int number) => "#" + number;

    private static string Format(string text) => text.Trim();
}
