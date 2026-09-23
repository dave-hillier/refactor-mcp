using System;

namespace Shop
{
    public class Employee
    {
    }

    public class Manager : Employee, IDisposable
    {
        public void Dispose()
        {
        }
    }
}
