namespace Shop
{
    public class Stock
    {
        private readonly int _unitWeight;

        public Stock(int unitWeight)
        {
            _unitWeight = unitWeight;
        }

        public double LoadPerTruck(int count, int trucks)
        {
            double totalWeight = count * _unitWeight;
            return /*^*/totalWeight / trucks;
        }
    }
}
