namespace Shop;

public partial class Customer
{
    public int Renames { get; private set; }

    partial void OnRenamed()
    {
        Renames++;
    }
}
