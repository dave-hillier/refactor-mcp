using System;

namespace Music
{
    public class Playlist : IDisposable
    {
        private readonly Catalogue _catalogue;

        public int Size => _catalogue.Size;

        public string Title { get => _catalogue.Title; set => _catalogue.Title = value; }

        public Playlist(string title)
        {
            _catalogue = new Catalogue(title);
        }

        public Playlist() : this("Untitled")
        {
        }

        public void Queue(string song) => _catalogue.Add(song);

        public void Dispose()
        {
        }
    }
}
