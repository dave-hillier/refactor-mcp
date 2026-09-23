namespace Shop
{
    public class Basket
    {
        private int _count;

        public int Count
        {
            get { return _count; }
            set { _count = value; }
        }

        public void Clear() => this._count = 0;
    }
}
