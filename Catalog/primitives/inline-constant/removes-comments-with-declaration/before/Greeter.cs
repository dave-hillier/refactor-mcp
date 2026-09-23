namespace Shop
{
    public class Greeter
    {
        /// <summary>What every message starts with.</summary>
        private const string Greeting = "Hello";

        public string Greet(string name)
        {
            // Always polite.
            return Greeting + ", " + name; // no punctuation
        }
    }
}
