namespace Shop
{
    public class Parcel
    {
        public string Code { get; set; }

        public decimal Weight { get; set; }

        public string Track(string prefix)
        {
            return prefix + Code + " (" + Weight + "kg)";
        }
    }
}
