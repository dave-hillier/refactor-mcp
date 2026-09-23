namespace Shop
{
    public static class Catalog
    {
        public static Index<int> ByNumber(Invoice invoice) => new Index<int>().With(1, invoice);
    }
}
