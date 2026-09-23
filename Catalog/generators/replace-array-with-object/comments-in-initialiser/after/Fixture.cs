namespace Shop
{
    public class Fixture
    {
        public string Title()
        {
            var teams = new Teams
            {
                Home = "Arsenal", // at home
                Away = "Chelsea"  // visiting
            };

            // Home side first.
            return teams.Home /* home */ + " v " + teams.Away;
        }
    }
}
