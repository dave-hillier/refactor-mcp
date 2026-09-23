namespace Shop
{
    public class MeterGauge : IGauge
    {
        private readonly Meter _adaptee;

        public MeterGauge(Meter adaptee)
        {
            _adaptee = adaptee;
        }

        public long Read(int channel) => _adaptee.Measure(channel);

        public double Scale => _adaptee.Factor;
    }
}
