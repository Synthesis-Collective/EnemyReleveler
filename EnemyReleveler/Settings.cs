namespace EnemyReleveler
{
    public class Settings
    {
        public int GlobalOffset { get; set; } = 0;
        public bool PrintDebugOutput { get; set; } = false;
        public EnemyRulesSettings EnemyRulesSettings { get; set; } = new();
    }

    public class EnemyRulesSettings
    {
        public bool UseLocalFile { get; set; } = false;
        public string FilePath { get; set; } = "C:\\enemy_rules.json";
    }
}