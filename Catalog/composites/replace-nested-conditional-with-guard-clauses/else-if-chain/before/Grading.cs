namespace School
{
    public class Grading
    {
        public string Grade(int score)
        {
            /*^*/if (score >= 90)
            {
                return "A";
            }
            else if (score >= 70)
            {
                return "B";
            }
            else
            {
                return "C";
            }
        }
    }
}
