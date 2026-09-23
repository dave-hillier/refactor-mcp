namespace Shop;

public partial class Customer
{
    public string Name { get; private set; } = "";

    public void Rename(string name)
    {
        Name = name;
        OnRenamed();
    }

    partial void OnRenamed();
}
