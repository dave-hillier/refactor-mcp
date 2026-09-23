namespace Shop
{
    public class Box<T>
    {
        public T Content { get; set; }

        public static string Describe(Box<T> box) => "Box of " + box.Content;
    }
}
