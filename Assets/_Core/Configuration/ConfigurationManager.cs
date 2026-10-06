namespace _Core.Configuration
{
    public static class ConfigurationManager
    {
        public static GameConfig GameConfig { get; private set; }

        public static void Initialize(GameConfig config)
        {
            GameConfig = config;
        }
    }
}