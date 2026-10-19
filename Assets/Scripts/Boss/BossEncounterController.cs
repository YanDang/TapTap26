using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameJam.Boss;

public sealed class BossEncounterController : MonoBehaviour
{
    public static BossEncounterController Instance { get; private set; }
    public BossBrain Boss { get; private set; }
    public static bool EncounterActive => Instance && Instance.Boss && Instance.Boss.Entity && Instance.Boss.Entity.IsAlive;
    public event Action<EnemyController> Defeated;
    bool notified;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if(!Instance) new GameObject("BossEncounter").AddComponent<BossEncounterController>(); }
    void Awake() { if(Instance && Instance!=this) { Destroy(gameObject); return; } Instance=this; DontDestroyOnLoad(gameObject); }
    IEnumerator Start()
    {
        bool test=SceneManager.GetActiveScene().name=="BossTest" || Array.IndexOf(Environment.GetCommandLineArgs(),"-tapboss")>=0;
        if(test && SceneManager.GetActiveScene().name!="BiomechanicalCombatScene") yield return SceneManager.LoadSceneAsync("BiomechanicalCombatScene");
        yield return null;
        if(test) StartEncounter();
    }
    void Update() {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(Input.GetKeyDown(KeyCode.F9)) StartEncounter();
#endif
    }
    public BossBrain StartEncounter()
    {
        if(BossDoorIntro.Active) return Boss;
        return SpawnEncounter(null, 0);
    }
    // Door director is the only formal gameplay caller. The parameterless development entry is retained.
    public BossBrain StartEncounter(Vector3 position, float introHold)
    {
        return SpawnEncounter(position, introHold);
    }
    public static float PresentationUnitScale(PlayerController player)
    {
        var scale=FindObjectOfType<TapCombatScale>();
        return (scale && scale.VisualHeight>0 ? scale.VisualHeight : TapCombatScale.HeroBounds(player).size.y)/2f;
    }
    BossBrain SpawnEncounter(Vector3? spawnPosition, float introHold)
    {
        if(EncounterActive) return Boss;
        if(EnemyController.CombatWorldPaused || !PlayerController.Instance || !FindObjectOfType<BiomechanicalCombatAdapter>()) return null;
        var prefab=Resources.Load<GameObject>("Boss/Boss");
        if(!prefab) { Debug.LogError("Boss production prefab missing; no substitute spawned"); return null; }
        var p=PlayerController.Instance;
        var root=new GameObject("Boss_终焉海神"); root.SetActive(false);
        Vector3 at=spawnPosition ?? (p.transform.position+Vector3.right*1.3f+Vector3.down*.7f);
        var map=IsometricPathfinder.Instance;
        if(!spawnPosition.HasValue && map && !map.IsWalkable(map.WorldToCell(at))) at=p.transform.position;
        root.transform.position=at;
        var visual=Instantiate(prefab,root.transform); visual.name="BossVisual";
        var animator=visual.GetComponentInChildren<BossAnimator>();
        float heroHeight=PresentationUnitScale(p)*2f;
        visual.transform.localScale=Vector3.one*(heroHeight*2.5f/animator.Height);
        var body=root.AddComponent<Rigidbody2D>(); body.bodyType=RigidbodyType2D.Kinematic; body.gravityScale=0;
        var collider=root.AddComponent<CircleCollider2D>(); collider.radius=heroHeight*.45f;
        var entity=root.AddComponent<EnemyController>(); entity.autoAttachSquid=false;
        entity.visualRoot=visual.transform; entity.autoRespawn=false; entity.enableClockQTE=false;
        // Source ordinary attacks deal 1–2 / HP 120: use the midpoint, i.e. 80 baseline hits.
        entity.maxHp=p.attackDamage*80; entity.currentHp=entity.maxHp;
        Boss=root.AddComponent<BossBrain>();
        root.SetActive(true);
        Boss.Initialize(entity,animator,heroHeight/2f);
        if(introHold>0) { Boss.HoldForIntro(introHold); animator.Vanish=1; }
        root.AddComponent<BossAudio>();
        root.AddComponent<BossMoveFx>();
        root.AddComponent<BossUltDirector>();
        var smallBar=root.transform.Find("HPBar"); if(smallBar) smallBar.gameObject.SetActive(false);
        root.AddComponent<BossCombatDisplay>().Brain=Boss;
        notified=false; entity.Died+=OnDefeated;
        return Boss;
    }
    void OnDefeated(EnemyController entity) { if(notified)return; notified=true; Debug.Log("[BossEncounter] 终焉海神 defeated once; no reward and no save"); Defeated?.Invoke(entity); }
    public void CancelEncounter() { if(Boss) Destroy(Boss.gameObject); Boss=null; }
    void OnDestroy() { if(Instance==this) Instance=null; }
}
