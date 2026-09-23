namespace Shop
{
    public class Box<T>
    {
        public T Item { get; set; }

        public string Describe(string label)
        {
            string text = label + ": " + Item;
            return text;
        }
    }
}
