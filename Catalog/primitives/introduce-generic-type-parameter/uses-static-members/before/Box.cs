namespace Shop
{
    public class Box
    {
        private Invoice _item = Invoice.Blank();

        public Invoice Take() => _item;
    }
}
