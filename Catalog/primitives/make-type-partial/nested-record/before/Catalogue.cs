namespace Shop;

public class Catalogue
{
    public record Entry(string Sku, decimal Price);

    public Entry First() => new Entry("A1", 2m);
}
