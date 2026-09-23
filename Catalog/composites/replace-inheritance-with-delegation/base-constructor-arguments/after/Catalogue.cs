using System.Collections.Generic;

namespace Music
{
    public class Catalogue
    {
        private readonly List<string> _songs = new List<string>();

        public Catalogue(string title)
        {
            Title = title;
        }

        public string Title { get; set; }

        public int Size => _songs.Count;

        public void Add(string song) => _songs.Add(song);
    }
}
