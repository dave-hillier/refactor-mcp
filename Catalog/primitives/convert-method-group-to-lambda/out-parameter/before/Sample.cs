public delegate bool Parser(string text, out int value);

public class Sample
{
    public int Run()
    {
        Parser parse = /*^*/TryParse;
        return parse("7", out var parsed) ? parsed : 0;
    }

    private static bool TryParse(string text, out int value) => int.TryParse(text, out value);
}
