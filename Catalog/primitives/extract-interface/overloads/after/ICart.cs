namespace Shop
{
    public interface ICart
    {
        void Add(string item);
        void Add(string item, int quantity);
    }
}
