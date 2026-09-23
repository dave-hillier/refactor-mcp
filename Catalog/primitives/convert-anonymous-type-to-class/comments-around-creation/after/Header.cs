using System;
using System.Collections.Generic;

namespace Shop
{
    public class Header
    {
        public string Render(string title, int count)
        {
            // One entry per page.
            var entry = new Entry(title, count); // shown at the top
            return entry.ToString();
        }
    }

    internal sealed class Entry
    {
        public Entry(string title, int count)
        {
            Title = title;
            Count = count;
        }

        public string Title { get; }

        public int Count { get; }

        public override bool Equals(object obj)
        {
            return obj is Entry other
                && EqualityComparer<string>.Default.Equals(Title, other.Title)
                && EqualityComparer<int>.Default.Equals(Count, other.Count);
        }

        public override int GetHashCode() => HashCode.Combine(Title, Count);

        public override string ToString() => $"{{ Title = {Title}, Count = {Count} }}";
    }

    public class Footer
    {
    }
}
