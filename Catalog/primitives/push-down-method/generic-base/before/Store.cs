namespace Shop
{
    public class Store<TItem>
    {
        public string Describe(TItem item) => "Item " + item;
    }
}
