using System;

namespace Shop
{
    public class Sample
    {
        public bool IsReady() => true;

        public Func<bool> Check() => IsReady;
    }
}
