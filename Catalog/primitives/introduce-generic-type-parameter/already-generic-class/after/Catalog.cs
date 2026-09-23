namespace Shop
{
    public static class Catalog
    {
        public static Index<int, Invoice> ByNumber(Invoice invoice) => new Index<int, Invoice>().With(1, invoice);
    }
}
