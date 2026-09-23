namespace Shop;

public sealed partial record Tag
{
    public Tag(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
