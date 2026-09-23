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

            Nicknames.Apply(nickname, name);
        }

        private INicknamesStrategy Nicknames => _flags.IsEnabled("Nicknames") ? new NicknamesStrategy() : new NoNicknamesStrategy();
    }

    internal interface INicknamesStrategy
    {
        void Apply(string nickname, string name);
    }

    internal sealed class NicknamesStrategy : INicknamesStrategy
    {
        public void Apply(string nickname, string name)
        {
            Console.WriteLine("Hello " + nickname.ToUpperInvariant());
        }
    }

    internal sealed class NoNicknamesStrategy : INicknamesStrategy
    {
        public void Apply(string nickname, string name)
        {
            Console.WriteLine("Hello " + name);
        }
    }
}
