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
            return TotalWeight(count) / trucks;
        }

        private double TotalWeight(int count)
        {
            return count * _unitWeight;
        }
    }
}
