namespace Shop;

partial record Tag
{
    public string Display => "#" + Name;
}
