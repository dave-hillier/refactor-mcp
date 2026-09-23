namespace Shop
{
    public class Parcel
    {
        public string Code { get; set; }

        public string Track(string prefix) => prefix + Code;
    }
}
