namespace Shop
{
    public class Document
    {
        public virtual string Title() => "Document";
    }

    public class Report : Document
    {
        public string Print() => "# " + base.Title();
    }

    public class AnnualReport : Report
    {
        public override string Title() => "Annual report";
    }
}
