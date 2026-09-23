namespace Shop
{
    public static class Dispatch
    {
        public static void ShipAll(Store store)
        {
            foreach (var order in store.Pending())
                order.Shipped = true;
        }
    }
}
