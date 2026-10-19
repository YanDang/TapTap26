/// <summary>Pure managed FINALE tuning; float HP units (not hearts), no rounding of damage.</summary>
public static class TapCorruptionRules
{
    public const int MaxLevel = 6;
    public static int LevelForDay(int day) => day <= 1 ? 0 : day >= 7 ? MaxLevel : day - 1;
    static int Clamp(int level) => level < 0 ? 0 : level > MaxLevel ? MaxLevel : level;
    public static float HpMultiplier(int level) => 1f + .25f * Clamp(level);
    public static float DamageMultiplier(int level) => 1f + .12f * Clamp(level);
}
