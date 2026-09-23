namespace Shop
{
    public class Greeter
    {
        private string? _trimmed;

        public int Length(string name)
        {
            _trimmed = name.Trim();
            return _trimmed.Length;
        }
    }
}
