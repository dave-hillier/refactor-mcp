namespace Shop
{
    public interface IGauge
    {
        long Read(int channel);

        double Scale { get; }
    }

    public class Meter
    {
        public float Factor { get; set; } = 1f;

        public int Measure(long channel) => (int)(channel * Factor);
    }
}
