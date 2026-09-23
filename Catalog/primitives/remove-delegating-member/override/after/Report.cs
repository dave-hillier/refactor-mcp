namespace Shop
{
    public class Document
    {
        public virtual string Title() => "Document";
    }

    public class Report : Document
    {
        public string Print() => "# " + Title();
    }
}
