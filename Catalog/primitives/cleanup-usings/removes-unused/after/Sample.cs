using System;
using System.Linq;

namespace Shop
{
    public class Sample
    {
        public int Count(int[] values) => values.Where(v => v>0).Count();

        public void Say()   => Console.WriteLine("Hi");
    }
}
