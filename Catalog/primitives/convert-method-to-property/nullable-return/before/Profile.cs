namespace Shop
{
    public class Profile
    {
        private string? _nickname;

        public string? GetNickname() => _nickname;

        public void Forget() => _nickname = null;

        public int Length() => GetNickname()?.Length ?? 0;
    }
}
