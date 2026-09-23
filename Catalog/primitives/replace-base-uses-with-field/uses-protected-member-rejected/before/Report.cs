namespace Shop
{
    public class Document
    {
        public string Body { get; set; } = "";

        protected string Stamp() => "[draft]";
    }

    public class Report : Document
    {
        private readonly Document _document = new Document();

        public string Print() => Stamp() + Body;
    }
}
