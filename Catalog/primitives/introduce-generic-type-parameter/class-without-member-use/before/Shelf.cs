namespace Shop
{
    public static class Shelf
    {
        public static decimal Roundtrip(Invoice invoice)
        {
            Box box = Box.Empty();
            box.Put(invoice);
            return box.Take().Total;
        }
    }
}
