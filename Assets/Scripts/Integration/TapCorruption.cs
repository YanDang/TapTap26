using UnityEngine;
using GameJam.Boss;

/// <summary>Spawn-day snapshot. EnemyController.Start is the common entry after factory configuration;
/// Awake/OnEnable are too early for the inactive-root Boss factory. No HP owner besides EnemyController.</summary>
[DisallowMultipleComponent]
public sealed class TapCorruption : MonoBehaviour
{
    public static readonly Color Tint = new Color(1f, .30f, .72f);
    public static readonly Color Glow = new Color(.82f, .12f, .80f);
    public static int CurrentDay => PlayerSessionData.greenhouse != null ? PlayerSessionData.greenhouse.day : 1;
    public int Level { get; private set; }
    public float BaseMaxHp { get; private set; }
    public float DamageMultiplier => TapCorruptionRules.DamageMultiplier(Level);
    public float Amount => Level / 6f;
    public Color BodyMultiplier => Level == 0 ? Color.white : Color.Lerp(Color.white, Tint, Mathf.Lerp(.35f, .85f, Amount));
    public float GlowStrength => Level == 0 ? 0 : Mathf.Lerp(.16f, .42f, Amount) * (.75f + .25f * (.5f + .5f * Mathf.Sin(Time.time * 3.2f)));
    public SpriteRenderer Pool { get; private set; }
    public ParticleSystem Motes { get; private set; }
    EnemyController owner;
    Material sporeMaterial;
    bool initialized;

    public static bool IsBoss(EnemyController enemy) => enemy && enemy.GetComponent<BossBrain>();
    public static TapCorruption Apply(EnemyController enemy, int day, float baseMaxHp = -1)
    {
        if (!enemy || IsBoss(enemy)) return null;
        var c = enemy.GetComponent<TapCorruption>() ?? enemy.gameObject.AddComponent<TapCorruption>();
        c.owner = enemy;
        enemy.Corruption = c;
        c.Configure(day, baseMaxHp);
        return c;
    }

    void Configure(int day, float baseMaxHp)
    {
        int level = TapCorruptionRules.LevelForDay(day);
        float basis = baseMaxHp > 0 ? baseMaxHp : initialized ? BaseMaxHp : owner.maxHp;
        if (initialized && Level == level && Mathf.Approximately(basis, BaseMaxHp)) return;
        float ratio = owner.maxHp > 0 ? Mathf.Clamp01(owner.currentHp / owner.maxHp) : 1;
        BaseMaxHp = basis;
        Level = level;
        initialized = true;
        owner.maxHp = BaseMaxHp * TapCorruptionRules.HpMultiplier(Level);
        owner.currentHp = owner.maxHp * ratio;
        owner.UpdateHealthBar();
        if (Level > 0) BuildFx();
        if (Pool) Pool.gameObject.SetActive(Level > 0);
        if (Motes)
        {
            var emission = Motes.emission; emission.rateOverTime = Mathf.Lerp(3, 16, Amount);
            if (Level > 0) Motes.Play(); else Motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void BuildFx()
    {
        if (Pool) return;
        var go = new GameObject("CorruptionPool"); go.transform.SetParent(transform, false);
        // Target world is XY: an ellipse, NOT the source project's XZ 90-degree rotation.
        go.transform.localPosition = new Vector3(0, .03f, .02f);
        go.transform.localScale = new Vector3(1.25f, .48f, 1);
        Pool = go.AddComponent<SpriteRenderer>(); Pool.sprite = BossFxAssets.Dot;
        Pool.sharedMaterial = BossFxAssets.Material; Pool.sortingOrder = 3;
        Pool.color = new Color(Glow.r, Glow.g, Glow.b, .3f);
        var motes = new GameObject("CorruptionMotes"); motes.transform.SetParent(transform, false);
        motes.transform.localPosition = new Vector3(0, .1f, -.02f);
        Motes = motes.AddComponent<ParticleSystem>();
        Motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = Motes.main; main.loop = true; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
        main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(.035f, .09f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, .25f, .7f, .8f), new Color(.6f, .1f, .9f, .7f));
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 60;
        var shape = Motes.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .35f;
        var velocity = Motes.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
        // All velocity axes use the same curve mode (Unity rejects mixed constant/two-constant).
        velocity.x = new ParticleSystem.MinMaxCurve(-.06f, .06f);
        velocity.y = new ParticleSystem.MinMaxCurve(.20f, .55f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var fade = Motes.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .25f), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        // Mesh/particle renderers do not auto-bind a Sprite's texture: explicit soft texture avoids squares.
        sporeMaterial = new Material(BossFxAssets.Material); sporeMaterial.mainTexture = BossFxAssets.Dot.texture;
        var renderer = motes.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = sporeMaterial; renderer.sortingOrder = 12;
    }

    void Update()
    {
        if (!owner) return;
        bool alive = owner.IsAlive && Level > 0;
        if (Pool) Pool.enabled = alive;
        if (Motes)
        {
            if (!alive && Motes.isPlaying) Motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            else if (alive && (EnemyController.isAIPaused || EnemyController.CombatWorldPaused)) Motes.Pause();
            else if (alive && !Motes.isPlaying) Motes.Play();
        }
        if (Pool && alive) Pool.color = new Color(Glow.r, Glow.g, Glow.b, Mathf.Lerp(.22f, .5f, Amount) * (.7f + .3f * Mathf.Sin(Time.time * 3.2f) * .5f + .15f));
    }
    void OnDisable() { if (Motes) Motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
    void OnDestroy() { if (sporeMaterial) Destroy(sporeMaterial); }
}
