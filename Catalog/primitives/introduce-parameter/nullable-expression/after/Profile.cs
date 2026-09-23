namespace Shop;

public class Profile
{
    public string Display(string? nickname, string label)
    {
        return label;
    }

    public string Card(string? name)
    {
        return Display("sam", "sam" ?? "anonymous") + Display(name, name ?? "anonymous");
    }
}
