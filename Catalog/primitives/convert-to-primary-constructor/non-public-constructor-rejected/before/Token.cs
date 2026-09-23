namespace Shop;

public class Token
{
    private readonly string _value;

    private Token(string value)
    {
        _value = value;
    }

    public static Token Create() => new Token("t");

    public string Value => _value;
}
