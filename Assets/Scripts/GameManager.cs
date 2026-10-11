using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Bootstraps the combat test on top of Hermes' Nirun Ecostation scene: camera rig + screen FX, HUD, player at
    /// the PlayerSpawn anchor, kickable tables near the BuildZone anchors, and enemy waves from the EnemyEntrance anchors.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;
        public PlayerController player;
        public bool autoWaves = true;
        public int humanoidsPerWave = 2, beastsPerWave = 2;
        public Color beastTint = new Color(1f, 0.55f, 0.62f);
        [Header("新主角（寄居蟹，2D 骨骼）")]
        public bool useHero2D = true;          // false = 回到旧的序列帧主角
        public float hero2DScale = 0.3f;       // 原图约 7 单位高，缩到与场景角色相当
        public float hero2DSpringInertia = 0.6f;
        [Header("战斗家具")]
        // 2026-10-11 用户：测试场景里的杂物和家具全部清掉，家具 / 桌子都从物品栏自己摆；下面几个开关默认关
        public bool spawnTables = false;       // 开局在场景里摆桌子（现在桌子在物品栏第 8 格）
        public bool spawnGasCysts = false;     // 气囊疙瘩：敌人走进去就范围爆炸
        public bool spawnScrapFurniture = false; // 另外 4 个废料：痉挛金属团 / 烂泥珊瑚泥潭 / 嘶鸣排气瓣 / 共生残渣路障
        public bool spawnFormalFurniture = false; // 4 个正式家具直接摆在场景里（2026-10-11 起改为从物品栏自己摆，默认关）
        [Header("敌人外观：鱿鱼怪（2D 骨骼）")]
        public bool useSquidEnemies = true;    // false = 回到旧的昆虫序列帧
        public float squidHeight = 3.2f;       // 鱿鱼在场景里的高度（世界单位）
        public float squidHover = 0.35f;       // 触须梢离地多高     // 头发对移动的反应强度

        SpriteSet _hero, _enemy;
        Material _spriteMat;
        Vector3 _spawn;
        readonly List<Vector3> _entrances = new List<Vector3>(), _buildZones = new List<Vector3>();
        float _nextWave = -1;
        Bounds _stationArea;
        SpriteRenderer _marker, _lockRing;
        float _markerT;

        void Awake()
        {
            I = this;
            if (!TimeManager.I) new GameObject("TimeManager").AddComponent<TimeManager>();
            if (SelfTest.Requested) { autoWaves = false; gameObject.AddComponent<SelfTest>(); }
            _hero = Resources.Load<SpriteSet>("SpriteSets/Hero");
            _enemy = Resources.Load<SpriteSet>("SpriteSets/Enemy");
            _spriteMat = Resources.Load<Material>("Materials/SpriteBillboard");
            ReadAnchors();
            SetupCamera();
            var hud = HUD.Build();
            // 物品栏 + 气系生产线（摆放模式、气体网络）；开局先给一批气系原料和正式家具试玩（还没有采集系统）
            Inventory.I = new Inventory();
            Inventory.I.Add(ItemId.GasVent, 2);
            Inventory.I.Add(ItemId.GasSac, 3);
            Inventory.I.Add(ItemId.GasDuct, 10);
            Inventory.I.Add(ItemId.EelGenerator, 1);
            Inventory.I.Add(ItemId.CoralWall, 2);
            Inventory.I.Add(ItemId.WaterTower, 1);
            Inventory.I.Add(ItemId.BioIncubator, 1);
            Inventory.I.Add(ItemId.Table, 2);
            InventoryBar.Build(hud.transform);
            gameObject.AddComponent<GasNetwork>();
            gameObject.AddComponent<BuildController>().Init(Resources.Load<Material>("Materials/SpriteBillboard"));
            gameObject.AddComponent<GameInput>();

            player = SpawnPlayer(_spawn);
            CameraRig.I.target = player.transform;
            CameraRig.I.Snap();

            if (spawnTables)
            {
                int n = 0;
                foreach (var b in _buildZones) if (n++ < 3) KickableTable.Build(b + new Vector3(1.5f, 0, -1f), 0);
                foreach (var off in new[] { new Vector3(2.4f, 0, 1.4f), new Vector3(-2.6f, 0, 2.6f) })
                {
                    var pos = _spawn + off;
                    bool clear = true;
                    foreach (var t in KickableTable.All) if (DirUtil.Flat(t.transform.position - pos).magnitude < 2.2f) clear = false;
                    if (clear) KickableTable.Build(pos, off.x > 0 ? 15 : -20);
                }
            }

            // 战斗家具（家具合成原型 Mix_v02 的废料）：在出生点周围找空地摆开，离桌子和彼此都留出距离
            if (spawnGasCysts)
            {
                PlaceFurniture(p => GasCyst.Build(p, _spriteMat), 4f, 2.2f);
                PlaceFurniture(p => GasCyst.Build(p, _spriteMat), 4f, 2.2f);
            }
            if (spawnScrapFurniture)
            {
                PlaceFurniture(p => SpasmMetal.Build(p, _spriteMat), 4.5f, 2.2f);
                PlaceFurniture(p => MudCoral.Build(p, _spriteMat), 5.5f, 1.8f);
                PlaceFurniture(p => SymbioticSludge.Build(p, _spriteMat), 5.5f, 2.4f);
                PlaceFurniture(p => ScreamingValve.Build(p, _spriteMat), 8f, 2.4f);   // 嘲讽范围大，放远一点
            }
            if (spawnFormalFurniture)
            {
                // 水塔和珊瑚墙挨着放（水汽里墙自愈快）；墙横在出生点外圈，挡住从外面过来的怪
                var tower = PlaceFurniture(p => WaterTower.Build(p, _spriteMat), 7f, 2.6f);
                if (tower) PlaceFurniture(p => CoralWall.Build(p, WallYaw(p), _spriteMat), 2.2f, 1.6f, tower.Pos);
                else PlaceFurniture(p => CoralWall.Build(p, WallYaw(p), _spriteMat), 6.5f, 3f);
                PlaceFurniture(p => EelGenerator.Build(p, _spriteMat), 6f, 2.4f);
                PlaceFurniture(p => BioIncubator.Build(p, _spriteMat), 9.5f, 2.6f);   // 安抚范围大，放远一点
            }

            _marker = FlatSprite("MoveMarker", "ui_move_marker", 0.8f, new Color(0.5f, 1f, 0.9f, 0f));
            _lockRing = FlatSprite("LockRing", "ui_ring", 1.4f, new Color(1f, 0.25f, 0.2f, 0.9f));
        }

        void Start()
        {
            if (autoWaves) SpawnWave();
        }

        void ReadAnchors()
        {
            _spawn = Vector3.zero;
            foreach (var a in FindObjectsOfType<SpotlightScene.SpotlightAnchor>())
            {
                var role = a.role ?? "";
                var p = DirUtil.Flat(a.transform.position);
                if (role.Contains("PlayerSpawn")) _spawn = p;
                else if (role.Contains("EnemyEntrance")) _entrances.Add(p);
                else if (role.Contains("BuildZone")) _buildZones.Add(p);
            }
            if (_entrances.Count == 0) { _entrances.Add(new Vector3(-8, 0, 4)); _entrances.Add(new Vector3(8, 0, 4)); }
            // 中央建筑的地基：怪物出生区（往里收 1 m，离墙远一点）
            var found = GameObject.Find("Station_Foundation");
            var r = found ? found.GetComponent<Renderer>() : null;
            if (r)
            {
                var b = r.bounds;
                _stationArea = new Bounds(new Vector3(b.center.x, 0, b.center.z), new Vector3(Mathf.Max(0.5f, b.size.x - 2f), 0, Mathf.Max(0.5f, b.size.z - 2f)));
            }
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (!cam)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.transform.SetPositionAndRotation(new Vector3(24, 28, -30), Quaternion.LookRotation(new Vector3(-24, -27.7f, 31)));
            }
            if (!cam.GetComponent<AudioListener>()) cam.gameObject.AddComponent<AudioListener>();
            if (!cam.GetComponent<AudioLowPassFilter>()) cam.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000;
            if (!cam.GetComponent<CameraRig>()) cam.gameObject.AddComponent<CameraRig>();
            if (!cam.GetComponent<ScreenFX>()) cam.gameObject.AddComponent<ScreenFX>();
            cam.transparencySortMode = TransparencySortMode.Orthographic;
        }

        // ---------------------------------------------------------------- spawning
        GameObject CharacterRoot(string n, Vector3 pos, SpriteSet set, bool unscaled, out SpriteAnimator anim, out SpriteRenderer sr, out CharacterController cc, float scale)
        {
            var root = new GameObject(n);
            root.layer = 10;
            root.transform.position = DirUtil.Flat(pos);
            cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 0.9f, 0); cc.height = 1.6f; cc.radius = 0.35f; cc.skinWidth = 0.05f;
            var vis = new GameObject("Visual");
            vis.transform.SetParent(root.transform, false);
            vis.transform.localScale = Vector3.one * scale;
            sr = vis.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = _spriteMat;
            var bb = vis.AddComponent<Billboard>();
            bb.anchor = root.transform; bb.towardCamera = 2f;
            anim = vis.AddComponent<SpriteAnimator>();
            anim.set = set; anim.sr = sr; anim.unscaled = unscaled;
            var sh = FlatSprite("Shadow", "fx_shadow", 1.1f * scale, new Color(1, 1, 1, 0.8f));
            sh.transform.SetParent(root.transform, false);
            sh.transform.localPosition = new Vector3(0, 0.02f, 0);
            return root;
        }

        public PlayerController SpawnPlayer(Vector3 pos)
        {
            var root = CharacterRoot("Player", pos, _hero, true, out var anim, out var sr, out var cc, 1f);
            var p = root.AddComponent<PlayerController>();
            p.anim = anim; p.sr = sr; p.cc = cc; p.spawnPoint = DirUtil.Flat(pos);
            anim.Play("Idle", Dir.Down, true);
            if (useHero2D) p.hero = AttachHero2D(root, sr);
            return p;
        }

        // 敌人外观换成鱿鱼：旧序列帧照常在后台跑（EnemyAI 用它计时），只是不显示。
        // 结构：SquidVisual（公告板 + SortingGroup）→ Squid 实例（缩放到 squidHeight，身体居中，触须梢离地 squidHover）
        // 各图层换成 SpriteBillboard 材质，Unit.ApplyFlash 会把受击闪白 / 红光预警作用到它们。
        void AttachSquid(GameObject root, EnemyAI e, Color tint, float sizeMul)
        {
            var prefab = Resources.Load<GameObject>("Squid/Squid");
            if (!prefab) { Debug.LogWarning("[Squid] Resources/Squid/Squid 不存在，继续用旧敌人"); return; }
            e.sr.enabled = false;

            var holder = new GameObject("SquidVisual");
            holder.transform.SetParent(root.transform, false);
            var bb = holder.AddComponent<Billboard>();
            bb.anchor = root.transform; bb.towardCamera = 2f;
            holder.AddComponent<UnityEngine.Rendering.SortingGroup>();

            var squid = Instantiate(prefab, holder.transform);
            squid.name = "Squid";
            // 用骨骼量尺寸（蒙皮包围盒在实例化当帧不可靠）
            float minY = float.MaxValue, maxY = float.MinValue, bodyX = 0f;
            foreach (var t in squid.GetComponentsInChildren<Transform>(true))
            {
                var lp = squid.transform.InverseTransformPoint(t.position);
                minY = Mathf.Min(minY, lp.y); maxY = Mathf.Max(maxY, lp.y);
                if (t.name == "身体根") bodyX = lp.x;
            }
            float k = squidHeight * sizeMul / Mathf.Max(maxY - minY, 1e-3f);
            squid.transform.localScale = Vector3.one * k;
            squid.transform.localPosition = new Vector3(-bodyX * k, -minY * k + squidHover, 0f);

            var rs = squid.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var r in rs) { r.sharedMaterial = _spriteMat; r.color = tint; }
            e.flashRenderers = rs;
            var anim = squid.GetComponent<SquidAnimator>();
            if (anim) anim.motion = root.transform;
            e.squid = anim;
            e.squidFace = squid.AddComponent<SquidFace>();    // 攻击 / 抓取时的愤怒表情
        }

        // 外观换成寄居蟹：旧序列帧照常在后台播放（PlayerController 用它的进度判定攻击 / 翻滚等状态），只是不显示。
        // 结构：Hero2DVisual（公告板，面向镜头 + SortingGroup 整体排序）→ Flip（转身时翻转、缩放）→ Hero2D 实例（脚底对齐原点、身体居中）
        HeroActions AttachHero2D(GameObject root, SpriteRenderer oldSprite)
        {
            var prefab = Resources.Load<GameObject>("Hero2D/Hero2D");
            if (!prefab) { Debug.LogWarning("[Hero2D] Resources/Hero2D/Hero2D 不存在，继续用旧主角"); return null; }
            oldSprite.enabled = false;

            var holder = new GameObject("Hero2DVisual");
            holder.transform.SetParent(root.transform, false);
            var bb = holder.AddComponent<Billboard>();
            bb.anchor = root.transform; bb.towardCamera = 2f;
            holder.AddComponent<UnityEngine.Rendering.SortingGroup>();

            var flip = new GameObject("Flip").transform;
            flip.SetParent(holder.transform, false);
            flip.localScale = Vector3.one * hero2DScale;

            var hero = Instantiate(prefab, flip);
            hero.name = "Hero2D";
            float footY = float.MaxValue, bodyX = 0f;
            foreach (var t in hero.GetComponentsInChildren<Transform>(true))
            {
                var lp = hero.transform.InverseTransformPoint(t.position);
                if (t.name.EndsWith("腿_目标")) footY = Mathf.Min(footY, lp.y);
                if (t.name == "身体根") bodyX = lp.x;
            }
            hero.transform.localPosition = new Vector3(-bodyX, footY == float.MaxValue ? 0f : -footY, 0f);

            var facing = hero.GetComponent<HeroFacing>();
            if (facing) { facing.target = flip; facing.motion = root.transform; }
            var gait = hero.GetComponent<HeroGait>();
            if (gait) { gait.motion = root.transform; gait.strideLength = 1.5f; gait.stepHeight = 0.45f; }   // 场景里跑得快，步子放大
            var springs = hero.GetComponent<HeroSpringBones>();
            if (springs) { springs.motion = root.transform; springs.inertiaScale = hero2DSpringInertia; }
            // 战斗动作层（攻击 / 蓄力 / 贝壳格挡 / 缩壳翻滚 + 伸臂重拳），运行时加上，不用重建预制体
            if (!hero.GetComponent<HeroFace>()) hero.AddComponent<HeroFace>();   // 表情（痛苦 / 高兴 / 严肃 / 愤怒）
            var actions = hero.GetComponent<HeroActions>();
            return actions ? actions : hero.AddComponent<HeroActions>();
        }

        public EnemyAI SpawnEnemy(EnemyKind kind, Vector3 pos)
        {
            float scale = kind == EnemyKind.Beast ? 1.12f : 1f;
            var root = CharacterRoot(kind == EnemyKind.Beast ? "Enemy_Beast" : "Enemy_Humanoid", pos, _enemy, false, out var anim, out var sr, out var cc, scale);
            if (kind == EnemyKind.Beast) sr.color = beastTint;
            var e = root.AddComponent<EnemyAI>();
            e.kind = kind; e.anim = anim; e.sr = sr; e.cc = cc; e.player = player;
            e.maxHp = e.hp = 8;
            anim.Play("Idle", Dir.Down, true);
            if (useSquidEnemies) AttachSquid(root, e, kind == EnemyKind.Beast ? beastTint : Color.white, scale);
            return e;
        }

        /// <summary>
        /// 怪物只从地图中央的建筑（贝壳生态站 Repairable_Shell_Ecostation）里出来（用户 2026-10-11）：
        /// 在站台地基范围内（往里收一圈，避开墙和控制台）随机找没被挡住的点。场景里没有这个建筑时退回旧的入口锚点。
        /// </summary>
        public Vector3 EnemySpawnPoint()
        {
            const int notCharacters = ~(1 << 10);
            if (_stationArea.size.sqrMagnitude > 0.01f)
                for (int tries = 0; tries < 20; tries++)
                {
                    var p = new Vector3(Random.Range(_stationArea.min.x, _stationArea.max.x), 0, Random.Range(_stationArea.min.z, _stationArea.max.z));
                    if (!Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.4f, 0.4f, notCharacters & ~(1 << 9), QueryTriggerInteraction.Ignore)) return p;
                }
            var ent = _entrances[Random.Range(0, _entrances.Count)];
            return Vector3.Lerp(ent, _spawn, 0.45f) + new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
        }

        public bool InStation(Vector3 p) => _stationArea.size.sqrMagnitude > 0.01f &&
                                            p.x >= _stationArea.min.x - 0.6f && p.x <= _stationArea.max.x + 0.6f &&
                                            p.z >= _stationArea.min.z - 0.6f && p.z <= _stationArea.max.z + 0.6f;

        public void SpawnWave()
        {
            for (int k = 0; k < humanoidsPerWave + beastsPerWave; k++)
            {
                var p = EnemySpawnPoint();
                // 鱿鱼一律是非人形（黄光可格挡 / 红光抓取），没有钟表 QTE
                SpawnEnemy(useSquidEnemies || k >= humanoidsPerWave ? EnemyKind.Beast : EnemyKind.Humanoid, p);
                Vfx.Spawn("VFX_ArrowBurst", p + Vector3.up * 0.6f, 0, 1f, false, null, new Color(1f, 0.55f, 0.65f));
            }
        }

        /// <summary>
        /// 在 center（默认出生点）周围半径 radius 的一圈上找一个离桌子、其他家具都至少 clearance 的位置摆家具。
        /// 放不下返回 null。
        /// </summary>
        CombatFurniture PlaceFurniture(System.Func<Vector3, CombatFurniture> build, float radius, float clearance, Vector3? center = null)
        {
            var c = center ?? _spawn;
            for (int ring = 0; ring < 4; ring++)
                for (int i = 0; i < 12; i++)
                {
                    float a = (i * 30f + ring * 15f + radius * 37f) * Mathf.Deg2Rad;
                    var pos = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (radius + ring * 1.5f);
                    bool clear = true;
                    foreach (var t in KickableTable.All) if (DirUtil.Flat(t.transform.position - pos).magnitude < clearance + 0.8f) clear = false;
                    foreach (var f in CombatFurniture.All)
                        if (f && DirUtil.Flat(f.Pos - pos).magnitude < clearance + (f is MudCoral m ? m.poolRadius : 0.6f)) clear = false;
                    if (!clear || !OnFlatFloor(pos, clearance)) continue;
                    return build(pos);
                }
            Debug.LogWarning($"[家具] 找不到空地放 {build.Method.Name}（半径 {radius}、间距 {clearance}）");
            return null;
        }

        /// <summary>珊瑚墙的走向：横在出生点外圈（和“出生点 → 墙”的方向垂直）。</summary>
        float WallYaw(Vector3 p)
        {
            var d = DirUtil.Flat(p - _spawn);
            return d.sqrMagnitude < 1e-4f ? 0f : Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg;
        }

        /// <summary>pos 是不是在平坦地面上（地面高度≈0、上方没有墙或道具）。场景里没有地面碰撞体时不检查。</summary>
        public bool OnFlatFloor(Vector3 pos, float clearance)
        {
            const int notCharacters = ~(1 << 10);
            if (!Physics.Raycast(_spawn + Vector3.up * 5f, Vector3.down, out var floor, 10f, notCharacters, QueryTriggerInteraction.Ignore))
                return true;                                                                                    // 场景没有地面碰撞体：不检查
            if (!Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var hit, 10f, notCharacters, QueryTriggerInteraction.Ignore))
                return false;                                                                                   // 地图外
            if (Mathf.Abs(hit.point.y - floor.point.y) > 0.08f) return false;                                   // 台子 / 道具上
            float r = Mathf.Min(clearance * 0.4f, 1f);
            return !Physics.CheckSphere(hit.point + Vector3.up * (r + 0.2f), r, notCharacters, QueryTriggerInteraction.Ignore);   // 旁边有墙（球离地，不碰地面）
        }

        SpriteRenderer FlatSprite(string n, string sprite, float size, Color c)
        {
            var go = new GameObject(n);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Resources.Load<Sprite>("UI/" + sprite);
            sr.sharedMaterial = _spriteMat;
            sr.color = c;
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            if (sr.sprite) go.transform.localScale = Vector3.one * (size / sr.sprite.bounds.size.x);
            return sr;
        }

        public void ShowMoveMarker(Vector3 p)
        {
            _marker.transform.position = p + Vector3.up * 0.03f;
            _markerT = 0.5f;
        }

        void Update()
        {
            if (autoWaves && EnemyAI.AliveCount == 0)
            {
                if (_nextWave < 0) _nextWave = Time.time + 3f;
                else if (Time.time >= _nextWave) { _nextWave = -1; SpawnWave(); }
            }
            _markerT -= Time.unscaledDeltaTime;
            var c = _marker.color; c.a = Mathf.Clamp01(_markerT * 2f); _marker.color = c;
            _marker.transform.localScale = Vector3.one * (0.8f / Mathf.Max(0.01f, _marker.sprite.bounds.size.x)) * (1.2f - 0.4f * Mathf.Clamp01(_markerT * 2f));

            var t = player ? player.LockTarget : null;
            _lockRing.enabled = t && !t.Dead;
            if (_lockRing.enabled) _lockRing.transform.position = t.Pos + Vector3.up * 0.04f;
        }
    }
}
