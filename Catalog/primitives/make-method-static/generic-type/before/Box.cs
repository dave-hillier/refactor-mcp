namespace Shop
{
    public class Box<T>
    {
        public T Content { get; set; }

        public string Describe() => "Box of " + Content;
    }
}
