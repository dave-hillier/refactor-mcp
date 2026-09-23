namespace Shop
{
    public class Document
    {
        protected string Footer() => "-- end --";
    }

    public class Report : Document
    {
        public string Print(string body) => body + "\n" + Footer();
    }
}
