using System;

namespace Shop
{
    public class Settings
    {
        public Feature Create(string name)
        {
            return new Feature { Name = name, IsDisabled = true };
        }

        public void Enable(Feature feature)
        {
            if (feature.IsDisabled)
            {
                feature.IsDisabled = false;
                Console.WriteLine(feature.Name);
            }
        }
    }
}
