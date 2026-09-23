namespace Shop
{
    public class Order
    {
        private readonly INotifier _notifier;

        public Order(INotifier notifier)
        {
            _notifier = notifier ?? NullNotifier.Instance;
        }

        public void Close()
        {
            _notifier.Dispose();
        }
    }
}
