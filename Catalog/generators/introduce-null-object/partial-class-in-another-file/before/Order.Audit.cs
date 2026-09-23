namespace Shop
{
    public partial class Order
    {
        public void Audit()
        {
            _logger?.Log("Audited");
        }

        public void Replace(Order other)
        {
            other._logger = _logger;
        }
    }
}
