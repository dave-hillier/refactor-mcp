namespace Shop
{
    public class Profile
    {
        public string? Nickname { get; set; }

        public static Profile Anonymous() => new Profile { Nickname = null };
    }
}
