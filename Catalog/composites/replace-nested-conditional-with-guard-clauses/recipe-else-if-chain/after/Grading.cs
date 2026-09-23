namespace School
{
    public class Grading
    {
        public string Grade(int score)
        {
            if (score >= 90)
            {
                return "A";
            }

            if (score >= 70)
            {
                return "B";
            }

            return "C";
        }
    }
}
