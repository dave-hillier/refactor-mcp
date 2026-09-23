namespace Shop
{
    public class Depot
    {
        public Warehouse Warehouse { get; } = new Warehouse();

        public string Label(Parcel parcel) => parcel.Track("Depot: ");
    }
}
