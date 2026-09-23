namespace Shop
{
    public class CacheAdapter : ICache
    {
        private readonly LegacyCache _adaptee;

        public CacheAdapter(LegacyCache adaptee)
        {
            _adaptee = adaptee;
        }

        public string? Get(string key) => _adaptee.Lookup(key);

        public void Put(string key, string? value) => _adaptee.Remember(key, value);
    }
}
