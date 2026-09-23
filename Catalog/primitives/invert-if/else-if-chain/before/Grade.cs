namespace Shop
{
    public class Grade
    {
        public string Of(int score)
        {
            /*^*/if (score < 50)
            {
                return "fail";
            }
            else if (score < 80)
            {
                return "pass";
            }
            else
            {
                return "merit";
            }
        }
    }
}
