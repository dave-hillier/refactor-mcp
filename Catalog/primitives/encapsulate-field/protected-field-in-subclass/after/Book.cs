namespace Shop
{
    public class Book : Product
    {
        public void Rename(string title)
        {
            DisplayTitle = title;
        }

        public string Spine() => DisplayTitle + " (paperback)";
    }
}
