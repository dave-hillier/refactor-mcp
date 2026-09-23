namespace Shop
{
    public class Profile
    {
        private readonly string? _fallback = "anonymous";

        public string? Display(string? name) => name ?? _fallback;
    }
}
