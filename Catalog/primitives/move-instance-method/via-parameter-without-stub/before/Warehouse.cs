namespace Shop
{
    public class Warehouse
    {
        public string Track(Parcel parcel, string prefix) => prefix + parcel.Code;

        public string TrackAll(Parcel first, Parcel second) => Track(first, "1: ") + Track(second, "2: ");
    }
}
