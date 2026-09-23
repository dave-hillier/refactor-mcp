public class Sample
{
    private string? _name;

    public int Length(string text)
    {
        _name = text.Trim();
        return _name.Length;
    }
}
