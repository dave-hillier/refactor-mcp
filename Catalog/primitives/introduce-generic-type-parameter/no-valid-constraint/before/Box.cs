namespace Shop
{
    public class Box
    {
        private Invoice _item;

        public void Put(Invoice item) => _item = item;

        public decimal Value() => _item.Total;
    }
}
