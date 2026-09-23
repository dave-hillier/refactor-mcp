// Clock helpers.
namespace Shop
{
    public class Clock
    {
        public System./*^*/DateTimeOffset Now() => System.DateTimeOffset.UtcNow;

        public bool IsPast(System.DateTimeOffset when) => when < Now();
    }
}
