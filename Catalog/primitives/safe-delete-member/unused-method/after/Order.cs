namespace Shop
{
    public class Order
    {
        public int Total() => 10;

        public int Tax() => Total() / 5;
    }
}
