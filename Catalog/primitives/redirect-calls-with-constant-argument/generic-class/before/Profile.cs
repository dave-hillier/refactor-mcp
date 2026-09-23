namespace Config
{
    public class Profile
    {
        public Setting<string> Create()
        {
            var setting = new Setting<string>();
            setting.Set("name", "Ada");
            setting.Set("title", "Countess");
            return setting;
        }
    }
}
