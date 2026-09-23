namespace Shop
{
    public class Document
    {
        public virtual string Title() => "Document";
    }

    public class Report : Document
    {
        public override string Title() => base.Title();

        public string Print() => "# " + Title();
    }
}
