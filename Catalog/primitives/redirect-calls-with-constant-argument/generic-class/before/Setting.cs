namespace Config
{
    public class Setting<T>
    {
        public T Name { get; private set; }

        public T Title { get; private set; }

        public void Set(string key, T value)
        {
            switch (key)
            {
                case "name":
                    SetName(value);
                    break;
                case "title":
                    SetTitle(value);
                    break;
            }
        }

        public void SetName(T value)
        {
            Name = value;
        }

        public void SetTitle(T value)
        {
            Title = value;
        }
    }
}
