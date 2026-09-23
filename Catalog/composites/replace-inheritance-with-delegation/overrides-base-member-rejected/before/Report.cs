namespace Shop
{
    public class Document
    {
        public virtual string Title() => "Document";

        public string Heading() => "== " + Title() + " ==";
    }

    public class Report : Document
    {
        public override string Title() => "Report";
    }
}
