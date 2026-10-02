using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

/// <summary>
/// 玩家胶囊体控制器（带 A* 自动寻路与障碍规避）：
/// - 支持鼠标/手机触控点击地面移动 (Click-to-Move)
/// - 集成 IsometricPathfinder 智能避障寻路：遇到放置的方块/栅栏自动绕道
/// - 自动过滤摄像机拖拽与 UI 交互防穿透
/// - 精准对齐等距方块最顶面高度
/// - 仅允许在有地面方块的区域移动，拦截虚空点击
/// - 包含移动目标光环特效、朝向翻转与微弹性行走动画
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Dependencies (依赖组件)")]
    public Grid grid;
    public Tilemap groundTilemap;
    public Tilemap buildTilemap;
    public IsometricPathfinder pathfinder;

    [Header("Movement Settings (移动设置)")]
    [Tooltip("移动速度 (单位/秒)")]
    public float moveSpeed = 4.0f;
    [Tooltip("地面方块高度偏移，使玩家站在方块顶面上")]
    public float groundHeightOffset = 0.5f;
    [Tooltip("是否允许点击移动（非建造模式下为 true）")]
    public bool canMove = true;

    [Header("Visuals (角色表现)")]
    [Tooltip("角色精灵渲染器")]
    public SpriteRenderer playerRenderer;
    [Tooltip("角色脚底阴影")]
    public Transform shadowTransform;
    [Tooltip("行走轻微弹跳幅度")]
    public float walkBobAmount = 0.07f;
    [Tooltip("行走弹跳频率")]
    public float walkBobFrequency = 14f;

    [Header("Target Marker (点击目标光环)")]
    public Sprite clickMarkerSprite;
    public Color markerColor = new Color(0.2f, 1f, 0.7f, 0.85f);
    public Color blockedMarkerColor = new Color(1f, 0.3f, 0.3f, 0.85f);

    [Header("Click Sensitivity (点击判定灵敏度)")]
    public float clickMaxDistance = 15f;
    public float clickMaxDuration = 0.35f;

    // 寻路航点列表
    private readonly List<Vector3> waypoints = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private bool isMoving = false;

    // 点击判定跟踪
    private Vector2 pointerDownPos;
    private float pointerDownTime;
    private bool isPointerDown = false;
    private bool hasMovedBeyondThreshold = false;

    // 目标指示器物体
    private GameObject markerObject;
    private SpriteRenderer markerRenderer;
    private float markerTimer = 0f;

    private Camera cam;

    void Awake()
    {
        cam = Camera.main;

        if (grid == null)
            grid = FindObjectOfType<Grid>();

        if (grid != null)
        {
            var tms = grid.GetComponentsInChildren<Tilemap>();
            if (groundTilemap == null)
                groundTilemap = System.Array.Find(tms, t => t.name == "Tilemap") ?? (tms.Length > 0 ? tms[0] : null);
            if (buildTilemap == null)
                buildTilemap = System.Array.Find(tms, t => t.name != "Tilemap") ?? groundTilemap;
        }

        if (pathfinder == null)
        {
            pathfinder = GetComponent<IsometricPathfinder>() ?? FindObjectOfType<IsometricPathfinder>();
            if (pathfinder == null)
            {
                pathfinder = gameObject.AddComponent<IsometricPathfinder>();
                pathfinder.groundTilemap = groundTilemap;
                pathfinder.buildTilemap = buildTilemap;
            }
        }

        CreateMarkerObject();
    }

    private void CreateMarkerObject()
    {
        markerObject = new GameObject("Player_Click_Marker");
        markerRenderer = markerObject.AddComponent<SpriteRenderer>();

        if (clickMarkerSprite != null)
        {
            markerRenderer.sprite = clickMarkerSprite;
        }

        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader != null)
        {
            markerRenderer.material = new Material(unlitShader);
        }

        markerRenderer.color = markerColor;
        markerRenderer.sortingOrder = 9;
        markerObject.SetActive(false);
    }

    void Update()
    {
        HandleInput();
        UpdateMovement();
        UpdateMarkerAnimation();
    }

    private void HandleInput()
    {
        if (!canMove)
        {
            isPointerDown = false;
            return;
        }

        // 手机触控
        if (Input.touchCount > 0)
        {
            if (Input.touchCount > 1)
            {
                isPointerDown = false;
                return;
            }

            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                if (IsPointerOverUI(touch.fingerId))
                {
                    isPointerDown = false;
                    return;
                }
                StartPointer(touch.position);
            }
            else if (touch.phase == TouchPhase.Moved)
            {
                CheckPointerMove(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                if (isPointerDown && !hasMovedBeyondThreshold && (Time.time - pointerDownTime) <= clickMaxDuration)
                {
                    OnGroundClicked(touch.position);
                }
                isPointerDown = false;
            }
            else if (touch.phase == TouchPhase.Canceled)
            {
                isPointerDown = false;
            }
        }
        else
        {
            // 鼠标操作
            if (Input.GetMouseButtonDown(0))
            {
                if (IsPointerOverUI(-1))
                {
                    isPointerDown = false;
                    return;
                }
                StartPointer(Input.mousePosition);
            }
            else if (Input.GetMouseButton(0) && isPointerDown)
            {
                CheckPointerMove(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (isPointerDown && !hasMovedBeyondThreshold && (Time.time - pointerDownTime) <= clickMaxDuration)
                {
                    OnGroundClicked(Input.mousePosition);
                }
                isPointerDown = false;
            }
        }
    }

    private void StartPointer(Vector2 screenPos)
    {
        pointerDownPos = screenPos;
        pointerDownTime = Time.time;
        isPointerDown = true;
        hasMovedBeyondThreshold = false;
    }

    private void CheckPointerMove(Vector2 currentScreenPos)
    {
        if (Vector2.Distance(pointerDownPos, currentScreenPos) > clickMaxDistance)
        {
            hasMovedBeyondThreshold = true;
        }
    }

    private bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
        if (fingerId >= 0)
            return EventSystem.current.IsPointerOverGameObject(fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }

    private void OnGroundClicked(Vector2 screenPosition)
    {
        if (cam == null || grid == null || groundTilemap == null || pathfinder == null) return;

        Vector3 worldPos = cam.ScreenToWorldPoint(screenPosition);
        worldPos.z = 0f;

        // 顶面坐标对齐转换，计算点击的目标网格
        Vector3 groundRayPos = new Vector3(worldPos.x, worldPos.y - groundHeightOffset, 0f);
        Vector3Int targetCell = grid.WorldToCell(groundRayPos);

        // 计算玩家当前所处的网格
        Vector3 playerRayPos = new Vector3(transform.position.x, transform.position.y - groundHeightOffset, 0f);
        Vector3Int startCell = grid.WorldToCell(playerRayPos);

        // 使用 A* 寻路计算规避障碍物的最优路径
        List<Vector3Int> pathCells = pathfinder.FindPath(startCell, targetCell);

        if (pathCells != null && pathCells.Count > 0)
        {
            // 成功寻路：生成世界航点
            waypoints.Clear();
            foreach (var cell in pathCells)
            {
                Vector3 center = groundTilemap.GetCellCenterWorld(cell);
                Vector3 pt = new Vector3(center.x, center.y + groundHeightOffset, 0f);
                waypoints.Add(pt);
            }

            currentWaypointIndex = 0;
            isMoving = true;

            // 目标光环置于最终目的地
            Vector3 finalDest = waypoints[waypoints.Count - 1];
            ShowMarker(finalDest, markerColor);
        }
        else
        {
            // 无法到达（孤岛或无路）
            if (groundTilemap.HasTile(targetCell))
            {
                Vector3 center = groundTilemap.GetCellCenterWorld(targetCell);
                Vector3 pt = new Vector3(center.x, center.y + groundHeightOffset, 0f);
                ShowMarker(pt, blockedMarkerColor);
            }
        }
    }

    private void ShowMarker(Vector3 position, Color color)
    {
        if (markerObject != null)
        {
            markerObject.transform.position = position;
            markerObject.transform.localScale = Vector3.one * 1.3f;
            markerRenderer.color = color;
            markerTimer = 0.8f;
            markerObject.SetActive(true);
        }
    }

    private void UpdateMovement()
    {
        if (!isMoving || waypoints.Count == 0 || currentWaypointIndex >= waypoints.Count)
        {
            if (playerRenderer != null)
            {
                playerRenderer.transform.localPosition = Vector3.zero;
            }
            return;
        }

        Vector3 currentTarget = waypoints[currentWaypointIndex];

        // 朝向翻转判定
        if (playerRenderer != null)
        {
            if (currentTarget.x > transform.position.x + 0.02f)
            {
                playerRenderer.flipX = false; // 朝右
            }
            else if (currentTarget.x < transform.position.x - 0.02f)
            {
                playerRenderer.flipX = true; // 朝左
            }
        }

        // 平滑移向当前航点
        transform.position = Vector3.MoveTowards(transform.position, currentTarget, moveSpeed * Time.deltaTime);

        // 行走轻微弹跳
        if (playerRenderer != null)
        {
            float bob = Mathf.Abs(Mathf.Sin(Time.time * walkBobFrequency)) * walkBobAmount;
            playerRenderer.transform.localPosition = new Vector3(0f, bob, 0f);
        }

        // 抵达当前航点判定
        if (Vector3.Distance(transform.position, currentTarget) < 0.03f)
        {
            transform.position = currentTarget;
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Count)
            {
                // 到达最终目的地
                isMoving = false;
                waypoints.Clear();
                if (playerRenderer != null)
                {
                    playerRenderer.transform.localPosition = Vector3.zero;
                }
            }
        }
    }

    public void StopMovement()
    {
        isMoving = false;
        waypoints.Clear();
        if (playerRenderer != null)
        {
            playerRenderer.transform.localPosition = Vector3.zero;
        }
    }

    private void UpdateMarkerAnimation()
    {
        if (markerObject == null || !markerObject.activeSelf) return;

        if (markerTimer > 0f)
        {
            markerTimer -= Time.deltaTime;
            float t = markerTimer / 0.8f;
            float scale = Mathf.Lerp(1.0f, 1.3f, t);
            markerObject.transform.localScale = new Vector3(scale, scale, 1f);

            Color c = markerRenderer.color;
            c.a = Mathf.Clamp01(t) * 0.85f;
            markerRenderer.color = c;

            if (markerTimer <= 0f)
            {
                markerObject.SetActive(false);
            }
        }
    }
}
