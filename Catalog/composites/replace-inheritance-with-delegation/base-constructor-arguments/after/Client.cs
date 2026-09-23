namespace Music
{
    public static class Client
    {
        public static int Run()
        {
            using var playlist = new Playlist("Road trip");
            playlist.Queue("Song");
            playlist.Title = playlist.Title + " (edited)";
            return playlist.Size;
        }
    }
}
