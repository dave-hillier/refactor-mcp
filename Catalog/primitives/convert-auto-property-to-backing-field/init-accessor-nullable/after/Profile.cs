namespace Shop
{
    public class Profile
    {
        private readonly string? _nickname;

        public string? Nickname
        {
            get => _nickname;
            init => _nickname = value;
        }
    }

    public class Signup
    {
        public Profile Create(string name) => new Profile { Nickname = name };
    }
}
