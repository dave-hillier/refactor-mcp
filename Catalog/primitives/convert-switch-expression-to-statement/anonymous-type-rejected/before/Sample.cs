using System;

namespace Shop
{
    public class Sample
    {
        public void Print(int code)
        {
            var item = code /*^*/switch
            {
                1 => new { Name = "one" },
                _ => new { Name = "other" },
            };
            Console.WriteLine(item.Name);
        }
    }
}
