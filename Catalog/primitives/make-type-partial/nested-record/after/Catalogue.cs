namespace Shop;

public class Catalogue
{
    public partial record Entry(string Sku, decimal Price);

    public Entry First() => new Entry("A1", 2m);
}
