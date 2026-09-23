namespace Shop
{
    public partial class Order
    {
        public partial int Weigh(int items);
    }

    public partial class Order
    {
        public partial int Weigh(int items)
        {
            int weight = items * 2;
            return weight;
        }
    }
}
