namespace Shipping
{
    public class Parcel
    {
        public Parcel(DeliverySpeed speed)
        {
            Speed = speed;
        }

        public DeliverySpeed Speed { get; }

        public bool IsFast() => Speed != DeliverySpeed.Standard;
    }
}
