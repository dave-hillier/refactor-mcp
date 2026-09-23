namespace Shop;

public sealed partial record Tag
{
    public string Display => "#" + Name;
}
