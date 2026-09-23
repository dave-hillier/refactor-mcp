namespace Shop
{
    public struct Customer
    {
        public string Street;
        public string Town;

        public string FormatAddress() => Street + ", " + Town;
    }
}
