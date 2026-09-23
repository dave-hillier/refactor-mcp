using System;

namespace Music
{
    public class Playlist : Catalogue, IDisposable
    {
        public Playlist(string title) : base(title)
        {
        }

        public Playlist() : this("Untitled")
        {
        }

        public void Queue(string song) => base.Add(song);

        public void Dispose()
        {
        }
    }
}
