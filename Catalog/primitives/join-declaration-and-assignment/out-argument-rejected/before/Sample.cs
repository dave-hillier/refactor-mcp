public class Sample
{
    public int Parse(string text)
    {
        int /*^*/value;
        int.TryParse(text, out value);
        return value;
    }
}
