using System;

namespace School
{
    public class Grading
    {
        public void Report(int score)
        {
            /*^*/if (score < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(score));
            }
            else if (score >= 50)
            {
                Console.WriteLine("pass");
            }
            else
            {
                Console.WriteLine("fail");
            }

            Console.WriteLine(score);
        }
    }
}
