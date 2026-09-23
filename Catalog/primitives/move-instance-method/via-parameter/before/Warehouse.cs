namespace Shop
{
    public class Warehouse
    {
        public string Track(Parcel parcel, string prefix)
        {
            return prefix + parcel.Code + " (" + parcel.Weight + "kg)";
        }
    }
}
