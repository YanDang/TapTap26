using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

/// <summary>
/// 简易等距建造系统：
/// 1. 第一次点击方格：在目标方格显示明亮绿色棱形高亮边框。
/// 2. 再次点击同一方格：在该方格放置选定的方块。
/// 3. 点击其他方格：切换高亮目标。
/// 4. 智能过滤手势：滑动/拖拽地图或双指缩放时不触发点击。
/// </summary>
public class BuildController : MonoBehaviour
{
    [Header("Tilemap & Grid (网格与瓦片图)")]
    public Grid grid;
    public Tilemap targetTilemap;

    [Header("Building Tile (用于放置的方块资源)")]
    [Tooltip("放置的方块资源 (可拖拽替换)")]
    public TileBase buildTile;

    [Header("Cursor Settings (高亮棱形光标设置)")]
    [Tooltip("绿色棱形光标精灵")]
    public Sprite highlightSprite;
    [Tooltip("光标颜色")]
    public Color highlightColor = new Color(0.2f, 1f, 0.4f, 1f);
    [Tooltip("高亮光标 Y 轴微调偏移")]
    public float cursorYOffset = 0f;
    [Tooltip("高亮图层排序 (确保悬浮在所有地块之上)")]
    public int highlightSortingOrder = 100;
    [Tooltip("是否启用呼吸脉冲微动画")]
    public bool enablePulseAnimation = true;

    [Header("Click Sensitivity (点击判定灵敏度)")]
    [Tooltip("最大允许手指/鼠标移动距离 (像素)，超过则判定为拖动地图而非点击")]
    public float clickMaxDistance = 15f;
    [Tooltip("最长点击判定时间 (秒)")]
    public float clickMaxDuration = 0.35f;

    // 当前选中的格子坐标
    private Vector3Int? selectedCell = null;

    // 光标物体与组件
    private GameObject highlightObject;
    private SpriteRenderer highlightRenderer;

    // 点击判定跟踪
    private Vector2 pointerDownPos;
    private float pointerDownTime;
    private bool isPointerDown = false;
    private bool hasMovedBeyondThreshold = false;

    private Camera cam;

    void Awake()
    {
        cam = Camera.main;

        if (grid == null)
            grid = FindObjectOfType<Grid>();

        if (targetTilemap == null && grid != null)
            targetTilemap = grid.GetComponentInChildren<Tilemap>();

        CreateHighlightCursor();
    }

    private void CreateHighlightCursor()
    {
        highlightObject = new GameObject("Build_Highlight_Cursor");
        highlightObject.transform.SetParent(transform);
        highlightRenderer = highlightObject.AddComponent<SpriteRenderer>();

        if (highlightSprite != null)
        {
            highlightRenderer.sprite = highlightSprite;
        }

        // 使用 URP Unlit 材质，保证绿色发光边框不受场景光照阴影影响，始终极度鲜亮醒目
        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader != null)
        {
            highlightRenderer.material = new Material(unlitShader);
        }

        highlightRenderer.color = highlightColor;
        highlightRenderer.sortingOrder = highlightSortingOrder;

        // 默认隐藏
        highlightObject.SetActive(false);
    }

    void Update()
    {
        HandleInput();
        UpdatePulseAnimation();
    }

    private void HandleInput()
    {
        // 手机触控检测
        if (Input.touchCount > 0)
        {
            // 如果是双指缩放，放弃点击判定
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
                    OnCellClicked(touch.position);
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
            // 电脑鼠标（仅鼠标左键用于选择与建造）
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
                    OnCellClicked(Input.mousePosition);
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

    private void OnCellClicked(Vector2 screenPosition)
    {
        if (cam == null || grid == null || targetTilemap == null) return;

        Vector3 worldPos = cam.ScreenToWorldPoint(screenPosition);
        worldPos.z = 0f; // 投影到 2D 平面
        Vector3Int cellPos = grid.WorldToCell(worldPos);

        // 如果点击的正是当前已高亮的格子 -> 确认放置方块！
        if (selectedCell.HasValue && selectedCell.Value == cellPos)
        {
            PlaceBlock(cellPos);
        }
        else
        {
            // 第一次点击该格子 -> 显示绿色高亮棱形
            SelectCell(cellPos);
        }
    }

    private void SelectCell(Vector3Int cellPos)
    {
        selectedCell = cellPos;

        Vector3 cellCenter = targetTilemap.GetCellCenterWorld(cellPos);
        highlightObject.transform.position = new Vector3(cellCenter.x, cellCenter.y + cursorYOffset, 0f);
        highlightObject.SetActive(true);
    }

    private void PlaceBlock(Vector3Int cellPos)
    {
        if (buildTile != null && targetTilemap != null)
        {
            targetTilemap.SetTile(cellPos, buildTile);

            Debug.Log($"[BuildSystem] 成功在格子 {cellPos} 放置了方块: {buildTile.name}");

            // 放置完成后清除高亮，等待下一次点击选择
            ClearSelection();
        }
    }

    public void ClearSelection()
    {
        selectedCell = null;
        if (highlightObject != null)
        {
            highlightObject.SetActive(false);
        }
    }

    private void UpdatePulseAnimation()
    {
        if (!enablePulseAnimation || highlightObject == null || !highlightObject.activeSelf) return;

        // 呼吸效果：温和的尺寸轻微脉冲
        float scale = 1f + Mathf.Sin(Time.time * 6f) * 0.04f;
        highlightObject.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
