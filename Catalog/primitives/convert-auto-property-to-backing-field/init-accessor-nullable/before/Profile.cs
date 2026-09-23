namespace Shop
{
    public class Profile
    {
        public string? Nickname { get; init; }
    }

    public class Signup
    {
        public Profile Create(string name) => new Profile { Nickname = name };
    }
}
