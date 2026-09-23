namespace Shop
{
    public class Document
    {
        public virtual string Title() => "Document";

        public string Heading() => "# " + Title();
    }

    public class Report : Document
    {
        private readonly Document _document = new Document();

        public override string Title() => "Report";

        public string Print() => Heading();
    }
}
