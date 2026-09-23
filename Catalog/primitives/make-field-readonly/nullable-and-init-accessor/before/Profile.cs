namespace Shop
{
    public class Profile
    {
        private string? _nickname;

        public string? Nickname
        {
            get => _nickname;
            init => _nickname = value;
        }
    }
}
