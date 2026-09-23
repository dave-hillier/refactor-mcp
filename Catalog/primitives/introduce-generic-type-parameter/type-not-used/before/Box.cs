namespace Shop
{
    public class Box
    {
        private Invoice _item;

        public void Put(Invoice item) => _item = item;

        public Invoice Take() => _item;

        public static Box Empty() => new Box();
    }
}
