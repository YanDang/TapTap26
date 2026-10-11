using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 摆放模式（物品栏选中物品后进入）：
    /// · 家具（喷口 / 气泡囊 / 4 个正式家具 / 桌子）：半透明虚影跟着鼠标，绿色 = 能放、红色 = 不能放；右键 / Esc 取消。
    ///   左键 → 主角播 1 s 的放置动画（掏壳 → 举起 → 抛出胶囊），抛出瞬间生成家具并播部署动画（FurnitureDeploy）；
    ///   抛出前被打断（受击 / 被抓）退还物品。有作用范围的家具（发电机 / 水塔 / 培养箱）虚影下面画出范围圈。
    ///   珊瑚墙：默认横在“主角 → 鼠标”方向上（挡在主角前面），滚轮每格转 45°；鼠标靠近已有墙的一头时吸附上去接成一排
    ///   （默认顺着那段墙继续延伸，滚轮拐弯）。预览是一条墙长的粗线。
    ///   限制：离主角 buildRange 内、平地、离其他家具至少 minSpacing（墙按整条墙算；墙和墙只能在接头处挨着）。
    /// · 充气导管：在一个气系家具上按下左键 → 拖到另一个气系家具上松开 → 连上。
    ///   限制：最长 GasPipe.MaxLength、每 2 米消耗 1 根导管、不能穿墙、两者没连过。预览线绿 / 红。
    /// 玩家摆的家具坏了就没了（不重生；桌子踢飞后照旧回到摆放的位置）。不合法时在物品栏上方显示原因（Message）。
    /// </summary>
    public class BuildController : MonoBehaviour
    {
        public static BuildController I;

        public float buildRange = 9f;
        public float minSpacing = 1.3f;
        public float snapRadius = 1.1f;          // 鼠标离气系家具多近算“指着它”
        public float wallSnapRadius = 1.4f;      // 鼠标离已有墙的一头多近就吸附过去
        public const float WallLength = 2.6f;    // 和 CoralWall.length 一致

        public ItemId? Selected { get; private set; }
        public bool Active => Selected.HasValue;
        public string Message => Time.unscaledTime < _msgUntil ? _msg : "";
        public string Hint => Selected == ItemId.GasDuct ? "在一个气系家具上按住左键，拖到另一个上松开　右键取消"
                            : Selected == ItemId.CoralWall ? "左键放置　靠近墙头自动接上　滚轮旋转 / 拐弯　右键 / Esc 取消"
                            : "左键放置　右键 / Esc 取消";
        public int placedNodes, placedPipes, placedFurniture;
        public float wallTurn;                   // 滚轮叠加的墙角度（度）

        /// <summary>一次摆放的落点：位置、朝向（墙用）、吸附到的墙。</summary>
        public struct Spot { public Vector3 pos; public float yaw; public CoralWall snap; }

        Material _mat;
        Transform _ghostRoot;
        SpriteRenderer _ghost, _ghostRing;
        LineRenderer _preview;
        GasNode _dragFrom, _hover;
        string _msg; float _msgUntil;

        public void Init(Material spriteMat)
        {
            I = this;
            _mat = spriteMat;
            _ghostRoot = new GameObject("BuildGhost").transform;
            var holder = new GameObject("Visual").transform;
            holder.SetParent(_ghostRoot, false);
            var bb = holder.gameObject.AddComponent<Billboard>(); bb.anchor = _ghostRoot; bb.towardCamera = 1.5f;
            _ghost = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            _ghost.transform.SetParent(holder, false);
            if (_mat) _ghost.sharedMaterial = _mat;
            // 作用范围圈（平铺在地上）
            _ghostRing = new GameObject("Range").AddComponent<SpriteRenderer>();
            _ghostRing.transform.SetParent(_ghostRoot, false);
            _ghostRing.transform.localPosition = new Vector3(0, 0.05f, 0);
            _ghostRing.transform.rotation = Quaternion.Euler(90, 0, 0);
            _ghostRing.sprite = Resources.Load<Sprite>("UI/ui_ring");
            if (_mat) _ghostRing.sharedMaterial = _mat;
            _ghostRoot.gameObject.SetActive(false);

            _preview = new GameObject("PipePreview").AddComponent<LineRenderer>();
            _preview.positionCount = 2;
            _preview.widthMultiplier = 0.16f;
            _preview.material = new Material(Shader.Find("Sprites/Default"));
            _preview.alignment = LineAlignment.View;
            _preview.numCapVertices = 3;
            _preview.enabled = false;
        }

        void Awake() { I = this; }

        void Say(string msg) { _msg = msg; _msgUntil = Time.unscaledTime + 1.6f; }

        static PlayerController Player => GameManager.I ? GameManager.I.player : null;

        /// <summary>家具的作用范围（虚影下面画圈；0 = 不画）。</summary>
        static float RangeOf(ItemId id)
        {
            switch (id)
            {
                case ItemId.EelGenerator: return 2.2f;
                case ItemId.WaterTower: return 3.2f;
                case ItemId.BioIncubator: return 4.5f;
                case ItemId.GasCyst: return 2.6f;          // 爆炸半径
                case ItemId.SpasmMetal: return 0.8f;       // 触发半径
                case ItemId.MudCoral: return 2.4f;         // 泥潭
                case ItemId.ScreamingValve: return 7f;     // 嘲讽范围
                default: return 0f;
            }
        }

        static Sprite IconOf(ItemId id) => Inventory.IconOf(id);

        // ---------------------------------------------------------------- 选择
        public void Select(ItemId id)
        {
            if (Inventory.I == null || Inventory.I.Count(id) <= 0) { Say($"{Inventory.InfoOf(id).name}用完了"); return; }
            if (!Inventory.InfoOf(id).placeable) { Say($"{Inventory.InfoOf(id).name}的战斗效果还没做，暂时不能摆"); return; }
            var p = Player;
            if (p) p.CancelLeftHold();
            Selected = id;
            _dragFrom = null;
            wallTurn = 0f;
            bool node = id != ItemId.GasDuct;
            _ghostRoot.gameObject.SetActive(node);
            _preview.enabled = false;
            if (node)
            {
                _ghost.sprite = IconOf(id);
                if (_ghost.sprite)
                {
                    float k = 0.8f / _ghost.sprite.bounds.size.y;
                    _ghost.transform.localScale = Vector3.one * k;
                    _ghost.transform.localPosition = new Vector3(0, 0.4f, 0);
                }
                float r = RangeOf(id);
                _ghostRing.enabled = r > 0f;
                if (r > 0f && _ghostRing.sprite) _ghostRing.transform.localScale = Vector3.one * (r * 2f / _ghostRing.sprite.bounds.size.x);
            }
        }

        public void Cancel()
        {
            Selected = null;
            _dragFrom = null;
            if (_ghostRoot) _ghostRoot.gameObject.SetActive(false);
            if (_preview) _preview.enabled = false;
        }

        // ---------------------------------------------------------------- 落点 / 规则（自测也直接调用）
        /// <summary>珊瑚墙放在 pos 时的走向：横在“主角 → pos”方向上（和它垂直），再加上滚轮转的角度。</summary>
        public float WallYaw(Vector3 pos)
        {
            var p = Player;
            var d = p ? DirUtil.Flat(pos - p.Pos) : Vector3.forward;
            float baseYaw = d.sqrMagnitude < 1e-4f ? 0f : Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg;
            return baseYaw + wallTurn;
        }

        static Vector3 AxisOf(float yaw) => Quaternion.Euler(0, yaw, 0) * Vector3.right;

        /// <summary>鼠标点在 mouse 时实际放在哪：墙靠近已有墙的一头会吸附（接成一排 / 拐角），其他家具就放在鼠标处。</summary>
        public Spot Resolve(ItemId id, Vector3 mouse)
        {
            mouse = DirUtil.Flat(mouse);
            if (id != ItemId.CoralWall) return new Spot { pos = mouse };
            CoralWall best = null; float bestS = 0f, bd = wallSnapRadius;
            foreach (var f in CombatFurniture.All)
                if (f is CoralWall w && w.Alive)
                    foreach (float s in new[] { -1f, 1f })
                    {
                        float d = DirUtil.Flat(w.End(s) - mouse).magnitude;
                        if (d < bd) { bd = d; best = w; bestS = s; }
                    }
            if (!best) return new Spot { pos = mouse, yaw = WallYaw(mouse) };
            // 接在 best 的这一头：默认顺着它延伸，滚轮拐弯；往鼠标那一侧长，但不能折回去压在原来的墙上
            float yaw = best.Yaw + wallTurn;
            var axis = AxisOf(yaw);
            var end = best.End(bestS);
            float sign = Vector3.Dot(axis, mouse - end) >= 0f ? 1f : -1f;
            if (Mathf.Abs(Vector3.Dot(axis, best.Axis)) > 0.5f) sign = Mathf.Sign(Vector3.Dot(axis, best.Axis) * bestS);   // 接近直线：只能往外延伸
            return new Spot { pos = end + axis * (sign * WallLength * 0.5f), yaw = yaw, snap = best };
        }

        /// <summary>只检查一个点（喷口 / 气泡囊用；旧接口）。</summary>
        public bool CanPlaceNode(Vector3 pos, out string why) => CanPlace(ItemId.GasVent, pos, out why);

        public bool CanPlace(ItemId id, Vector3 mouse, out string why) => CanPlaceAt(id, Resolve(id, mouse), out why);

        public bool CanPlaceAt(ItemId id, Spot spot, out string why)
        {
            var pos = DirUtil.Flat(spot.pos);
            var p = Player;
            if (p && DirUtil.Flat(pos - p.Pos).magnitude > buildRange) { why = "离主角太远了"; return false; }
            bool isWall = id == ItemId.CoralWall;
            var axis = AxisOf(spot.yaw);
            // 墙沿墙身取 9 个点检查，其他家具只检查一个点
            int n = isWall ? 9 : 1;
            for (int i = 0; i < n; i++)
            {
                float t = isWall ? (i / (n - 1f) - 0.5f) : 0f;          // -0.5 .. 0.5
                var q = pos + axis * (t * WallLength);
                bool endPart = isWall && Mathf.Abs(t) >= 0.375f;
                foreach (var f in CombatFurniture.All)
                {
                    if (!f || !f.Alive || f is GasPipe) continue;
                    float d = DirUtil.Flat(f.AimPoint(q) - q).magnitude;
                    if (isWall && f is CoralWall w)
                    {   // 墙和墙：只能在接头处碰在一起
                        if (d >= 0.5f) continue;
                        bool atJoint = endPart && (DirUtil.Flat(w.End(-1) - q).magnitude < 0.7f || DirUtil.Flat(w.End(1) - q).magnitude < 0.7f);
                        if (atJoint) continue;
                        why = "和别的墙重叠了"; return false;
                    }
                    float need = (isWall ? 0.8f : minSpacing) + (f is MudCoral m ? m.poolRadius : 0f);
                    if (d < need) { why = "离其他家具太近"; return false; }
                }
                foreach (var tb in KickableTable.All)
                    if (DirUtil.Flat(tb.transform.position - q).magnitude < minSpacing + 0.5f) { why = "离桌子太近"; return false; }
                if (p && (isWall || id == ItemId.Table) && DirUtil.Flat(p.Pos - q).magnitude < 0.9f) { why = "会压到主角"; return false; }
                if (GameManager.I && !GameManager.I.OnFlatFloor(q, 1.2f)) { why = "这里放不下"; return false; }
            }
            why = ""; return true;
        }

        // ---------------------------------------------------------------- 摆放
        /// <summary>生成物品对应的家具（不检查、不扣物品）。</summary>
        GameObject Spawn(ItemId id, Spot s)
        {
            Component c;
            switch (id)
            {
                case ItemId.GasVent: c = GasVent.Build(s.pos, _mat); placedNodes++; break;
                case ItemId.GasSac: c = GasSac.Build(s.pos, _mat); placedNodes++; break;
                case ItemId.EelGenerator: c = EelGenerator.Build(s.pos, _mat); break;
                case ItemId.CoralWall: c = CoralWall.Build(s.pos, s.yaw, _mat); break;
                case ItemId.WaterTower: c = WaterTower.Build(s.pos, _mat); break;
                case ItemId.BioIncubator: c = BioIncubator.Build(s.pos, _mat); break;
                case ItemId.GasCyst: c = GasCyst.Build(s.pos, _mat); break;
                case ItemId.SpasmMetal: c = SpasmMetal.Build(s.pos, _mat); break;
                case ItemId.MudCoral: c = MudCoral.Build(s.pos, _mat); break;
                case ItemId.ScreamingValve: c = ScreamingValve.Build(s.pos, _mat); break;
                case ItemId.SymbioticSludge: c = SymbioticSludge.Build(s.pos, _mat); break;
                default: c = KickableTable.Build(s.pos, WallYaw(s.pos) - wallTurn); break;   // 桌子横着摆，长边对着主角
            }
            if (c is CombatFurniture f && !(f is GasNode)) { f.respawnSeconds = 0f; f.destroyOnBreak = true; placedFurniture++; }
            Sfx.Play("sfx_table_land", 0.5f);
            return c.gameObject;
        }

        /// <summary>立刻摆一件（不播主角动作，家具照样播部署动画；自测 / 演示用）。返回生成的物体。</summary>
        public GameObject PlaceNow(ItemId id, Vector3 mouse)
        {
            if (id == ItemId.GasDuct || !Inventory.InfoOf(id).placeable) return null;
            var s = Resolve(id, mouse);
            if (!CanPlaceAt(id, s, out var why)) { Say(why); return null; }
            if (!Inventory.I.Take(id, 1)) { Say($"{Inventory.InfoOf(id).name}用完了"); return null; }
            var go = Spawn(id, s);
            FurnitureDeploy.Begin(go, IconOf(id), null);
            return go;
        }

        public CombatFurniture PlaceItem(ItemId id, Vector3 mouse) { var go = PlaceNow(id, mouse); return go ? go.GetComponent<CombatFurniture>() : null; }

        /// <summary>喷口 / 气泡囊（旧接口，GasLineDemo 用）。</summary>
        public GasNode PlaceNode(ItemId id, Vector3 pos) =>
            id == ItemId.GasVent || id == ItemId.GasSac ? PlaceItem(id, pos) as GasNode : null;

        /// <summary>
        /// 玩家点左键摆放：检查 → 扣物品 → 主角播放置动作，抛出瞬间生成家具 + 部署动画（胶囊从钳子飞过去）。
        /// 抛出前被打断就退还物品。返回有没有开始。
        /// </summary>
        public bool RequestPlace(ItemId id, Vector3 mouse)
        {
            if (id == ItemId.GasDuct || !Inventory.InfoOf(id).placeable) return false;
            var p = Player;
            if (p && p.Placing) return false;                    // 上一件还没放完：这次点击不算
            var s = Resolve(id, mouse);
            if (!CanPlaceAt(id, s, out var why)) { Say(why); return false; }
            if (!Inventory.I.Take(id, 1)) { Say($"{Inventory.InfoOf(id).name}用完了"); return false; }
            var taken = Inventory.I.LastTaken;                  // 合成产物：扣掉的那一件（退还时原样放回）
            if (!p) { FurnitureDeploy.Begin(Spawn(id, s), IconOf(id), null); return true; }
            bool started = p.BeginPlace(s.pos,
                () =>
                {
                    if (!CanPlaceAt(id, s, out var why2)) { Inventory.I.Refund(id, taken); Say(why2); return; }   // 放的过程中被占了
                    FurnitureDeploy.Begin(Spawn(id, s), IconOf(id), p.PlaceHandPos);
                },
                () => { Inventory.I.Refund(id, taken); Say("被打断了，没放出去"); });
            if (!started) { Inventory.I.Refund(id, taken); Say("现在腾不出手"); }
            return started;
        }

        public bool CanConnect(GasNode a, GasNode b, out string why)
        {
            if (!a || !b || a == b || !a.Alive || !b.Alive) { why = "要连在两个不同的气系家具之间"; return false; }
            if (a.ConnectedTo(b)) { why = "这两个已经连上了"; return false; }
            float len = DirUtil.Flat(b.Pos - a.Pos).magnitude;
            if (len > GasPipe.MaxLength) { why = $"太长了（{len:F1} 米，最长 {GasPipe.MaxLength:F0} 米）"; return false; }
            int cost = GasPipe.DuctCost(len);
            if (Inventory.I.Count(ItemId.GasDuct) < cost) { why = $"导管不够（需要 {cost} 根）"; return false; }
            const int notCharacters = ~(1 << 10);
            if (Physics.Linecast(a.Pos + Vector3.up * 0.5f, b.Pos + Vector3.up * 0.5f, notCharacters, QueryTriggerInteraction.Ignore))
            { why = "中间被挡住了"; return false; }
            why = ""; return true;
        }

        public GasPipe PlacePipe(GasNode a, GasNode b)
        {
            if (!CanConnect(a, b, out var why)) { Say(why); return null; }
            Inventory.I.Take(ItemId.GasDuct, GasPipe.DuctCost(DirUtil.Flat(b.Pos - a.Pos).magnitude));
            placedPipes++;
            Sfx.Play("sfx_swing", 0.6f);
            return GasPipe.Connect(a, b);
        }

        // ---------------------------------------------------------------- 输入（GameInput 在摆放模式时调用）
        static bool MouseGround(out Vector3 p)
        {
            p = Vector3.zero;
            var cam = Camera.main;
            if (!cam) return false;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!new Plane(Vector3.up, 0).Raycast(ray, out float d)) return false;
            p = ray.GetPoint(d);
            return true;
        }

        GasNode NodeNear(Vector3 p)
        {
            GasNode best = null; float bd = snapRadius;
            foreach (var f in CombatFurniture.All)
                if (f is GasNode n && n.Alive)
                {
                    float d = DirUtil.Flat(n.Pos - p).magnitude;
                    if (d < bd) { bd = d; best = n; }
                }
            return best;
        }

        public void HandleInput()
        {
            if (!Active) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
            if (Input.GetMouseButtonDown(1)) { if (_dragFrom) _dragFrom = null; else Cancel(); return; }
            if (!MouseGround(out var m)) return;
            var id = Selected.Value;
            if (id != ItemId.GasDuct)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (id == ItemId.CoralWall && Mathf.Abs(wheel) > 0.01f) wallTurn = Mathf.Repeat(wallTurn + Mathf.Sign(wheel) * 45f, 360f);
                if (Input.GetMouseButtonDown(0) && RequestPlace(id, m) && Inventory.I.Count(id) <= 0) Cancel();
                return;
            }
            if (Input.GetMouseButtonDown(0))
            {
                _dragFrom = NodeNear(m);
                if (!_dragFrom) Say("从一个气系家具（喷口 / 气泡囊）开始拖");
            }
            if (Input.GetMouseButtonUp(0) && _dragFrom)
            {
                var to = NodeNear(m);
                if (to && to != _dragFrom) PlacePipe(_dragFrom, to);
                _dragFrom = null;
                if (Inventory.I.Count(ItemId.GasDuct) <= 0) Cancel();
            }
        }

        // ---------------------------------------------------------------- 预览
        void Update()
        {
            if (!Active) return;
            if (!MouseGround(out var m)) return;
            var id = Selected.Value;
            if (id != ItemId.GasDuct)
            {
                var s = Resolve(id, m);
                _ghostRoot.position = DirUtil.Flat(s.pos);
                bool ok = CanPlaceAt(id, s, out _);
                var c = ok ? new Color(0.6f, 1f, 0.6f, 0.6f) : new Color(1f, 0.4f, 0.4f, 0.5f);
                _ghost.color = c;
                _ghostRing.color = new Color(c.r, c.g, c.b, 0.45f);
                if (id == ItemId.CoralWall)
                {   // 墙：一条墙长的粗线（吸附到墙头时颜色偏青）
                    var axis = AxisOf(s.yaw) * WallLength * 0.5f;
                    _preview.enabled = true;
                    _preview.widthMultiplier = 0.5f;
                    _preview.SetPosition(0, s.pos - axis + Vector3.up * 0.1f);
                    _preview.SetPosition(1, s.pos + axis + Vector3.up * 0.1f);
                    var lc = s.snap && ok ? new Color(0.5f, 1f, 0.95f, 0.65f) : new Color(c.r, c.g, c.b, 0.55f);
                    _preview.startColor = _preview.endColor = lc;
                }
                return;
            }
            _preview.widthMultiplier = 0.16f;
            _hover = NodeNear(m);
            if (!_dragFrom) { _preview.enabled = false; return; }
            var end = _hover && _hover != _dragFrom ? _hover.Pos : DirUtil.Flat(m);
            _preview.enabled = true;
            _preview.SetPosition(0, _dragFrom.Pos + Vector3.up * 0.3f);
            _preview.SetPosition(1, end + Vector3.up * 0.3f);
            bool valid = _hover && _hover != _dragFrom && CanConnect(_dragFrom, _hover, out _);
            float len = DirUtil.Flat(end - _dragFrom.Pos).magnitude;
            var pc = valid ? new Color(0.5f, 1f, 0.6f, 0.85f) : len > GasPipe.MaxLength ? new Color(1f, 0.35f, 0.35f, 0.85f) : new Color(1f, 1f, 1f, 0.6f);
            _preview.startColor = _preview.endColor = pc;
        }
    }
}
