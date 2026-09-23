namespace Shop
{
    public interface ITitled
    {
        string GetTitle();
    }

    public class Book : ITitled
    {
        public string GetTitle() => "Emma";
    }
}
