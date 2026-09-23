namespace Shop
{
    public class Basket
    {
        public int Count { get; set; }

        public void Clear() => this.Count = 0;
    }
}
