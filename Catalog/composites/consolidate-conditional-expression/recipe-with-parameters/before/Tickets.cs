namespace Venue
{
    public static class Tickets
    {
        public static decimal Fee(int age, bool member)
        {
            var senior = age >= 65;
            /*^*/if (senior)
            {
                return 0m;
            }

            if (member && age < 18)
            {
                return 0m;
            }

            return 10m;
        }
    }
}
