namespace Shop
{
    public class Document
    {
        public virtual string Title() => "Document";
    }

    public class Order : Document
    {
        public override string Title() => "Order";

        public string Heading(int number)
        {
            string text = base.Title() + " " + number;
            return text;
        }
    }
}
