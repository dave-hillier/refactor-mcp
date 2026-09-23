namespace Shop
{
    public class Profile
    {
        private string? _nickname;

        public string? Nickname => _nickname;

        public void Forget() => _nickname = null;

        public int Length() => Nickname?.Length ?? 0;
    }
}
