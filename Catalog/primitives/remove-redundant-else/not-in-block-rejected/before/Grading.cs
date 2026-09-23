namespace School
{
    public class Grading
    {
        public string Grade(int score, bool resit)
        {
            if (resit)
            {
                score -= 10;
            }
            else /*^*/if (score >= 90)
            {
                return "A";
            }
            else
            {
                score += 5;
            }

            return score >= 50 ? "pass" : "fail";
        }
    }
}
