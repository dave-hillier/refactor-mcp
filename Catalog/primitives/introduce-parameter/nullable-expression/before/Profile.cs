namespace Shop;

public class Profile
{
    public string Display(string? nickname)
    {
        return /*[*/nickname ?? "anonymous"/*]*/;
    }

    public string Card(string? name)
    {
        return Display("sam") + Display(name);
    }
}
