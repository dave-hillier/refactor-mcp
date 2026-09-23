using System.Collections.Generic;

namespace Shop
{
    public class Order
    {
        private readonly List<string> _tags = new List<string>();

        public IReadOnlyList<string> Tags => _tags.AsReadOnly();

        public void AddTag(string tag) => _tags.Add(tag);

        public bool RemoveTag(string tag) => _tags.Remove(tag);

        public bool IsGift() => _tags.Contains("gift");
    }
}
