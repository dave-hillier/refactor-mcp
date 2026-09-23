namespace Shop
{
    public static class Dispatch
    {
        public static int Count(Store store) => store.Pending().Count;
    }
}
