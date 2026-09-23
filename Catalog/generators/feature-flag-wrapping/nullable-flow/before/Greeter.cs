using System;

namespace Shop
{
    public interface IFeatureFlags
    {
        bool IsEnabled(string flag);
    }

    public class Greeter
    {
        private readonly IFeatureFlags _flags;

        public Greeter(IFeatureFlags flags)
        {
            _flags = flags;
        }

        public void Greet(string? nickname, string name)
        {
            if (nickname is null)
                return;

            if (_flags.IsEnabled("Nicknames"))
            {
                Console.WriteLine("Hello " + nickname.ToUpperInvariant());
            }
            else
            {
                Console.WriteLine("Hello " + name);
            }
        }
    }
}
