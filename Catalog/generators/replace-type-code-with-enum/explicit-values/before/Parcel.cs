namespace Shipping
{
    public class Parcel
    {
        public const int Standard = 1;
        public const int Express = 2;
        public const int Overnight = 4;

        public Parcel(int speed)
        {
            Speed = speed;
        }

        public int Speed { get; }

        public bool IsFast() => Speed != Standard;
    }
}
