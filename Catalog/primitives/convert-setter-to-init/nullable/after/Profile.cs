namespace Shop
{
    public class Profile
    {
        public string? Nickname { get; init; }

        public static Profile Anonymous() => new Profile { Nickname = null };
    }
}
