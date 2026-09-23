#nullable enable

namespace Staff
{
    public class Employee : Person
    {
        public string Display() => Nickname ?? "anonymous";
    }
}
