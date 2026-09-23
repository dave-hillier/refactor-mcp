using System;

namespace Shop
{
    public class Settings
    {
        public Feature Create(string name)
        {
            return new Feature { Name = name, IsEnabled = false };
        }

        public void Enable(Feature feature)
        {
            if (!feature.IsEnabled)
            {
                feature.IsEnabled = true;
                Console.WriteLine(feature.Name);
            }
        }
    }
}
