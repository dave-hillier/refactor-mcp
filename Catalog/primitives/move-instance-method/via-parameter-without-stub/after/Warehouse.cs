namespace Shop
{
    public class Warehouse
    {
        public string TrackAll(Parcel first, Parcel second) => first.Track("1: ") + second.Track("2: ");
    }
}
