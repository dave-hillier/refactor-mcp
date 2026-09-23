#nullable enable

namespace Staff
{
    public class Employee : Person
    {
        public new string? Nickname => base.Nickname;

        public string Display() => Nickname ?? "anonymous";
    }
}
