namespace Shop
{
    public static class Shelf
    {
        public static decimal Roundtrip(Invoice invoice)
        {
            Box<Invoice> box = Box<Invoice>.Empty();
            box.Put(invoice);
            return box.Take().Total;
        }
    }
}
