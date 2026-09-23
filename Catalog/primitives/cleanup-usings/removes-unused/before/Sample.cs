using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Shop
{
    public class Sample
    {
        public int Count(int[] values) => values.Where(v => v>0).Count();

        public void Say()   => Console.WriteLine("Hi");
    }
}
