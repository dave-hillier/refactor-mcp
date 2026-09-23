namespace Config
{
    public class Profile
    {
        public Setting<string> Create()
        {
            var setting = new Setting<string>();
            setting.SetName("Ada");
            setting.Set("title", "Countess");
            return setting;
        }
    }
}
