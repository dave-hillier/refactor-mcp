namespace Shop;

public sealed partial class Tag
{
    public Tag(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
