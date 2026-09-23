namespace Shop
{
    public class Book : Product
    {
        public void Rename(string title)
        {
            _title = title;
        }

        public string Spine() => _title + " (paperback)";
    }
}
