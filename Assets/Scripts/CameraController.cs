using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 跨平台 2D/等距摄像机控制器：
/// - 手机端：单指丝滑 1:1 跟手拖拽，双指捏合缩放
/// - 电脑端：鼠标按键拖拽，鼠标滚轮缩放
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings (缩放设置)")]
    [Tooltip("最小视野 (最放大)")]
    public float minZoom = 2f;
    [Tooltip("最大视野 (最缩小)")]
    public float maxZoom = 12f;
    [Tooltip("电脑鼠标滚轮缩放速度")]
    public float mouseScrollSpeed = 1.2f;
    [Tooltip("手机双指缩放灵敏度")]
    public float pinchSensitivity = 2.0f;
    [Tooltip("缩放平滑阻尼时间 (秒)")]
    public float zoomSmoothTime = 0.03f;

    [Header("Pan Settings (平移设置)")]
    [Tooltip("电脑鼠标拖拽按键 (0: 左键, 1: 右键, 2: 中键)")]
    public int mouseDragButton = 0;
    [Tooltip("平移平滑时间 (越小越即时跟手)")]
    public float panSmoothTime = 0.02f;

    [Header("Bounds (边界范围限制，可选)")]
    public bool useBounds = false;
    public Vector2 minBounds = new Vector2(-20f, -20f);
    public Vector2 maxBounds = new Vector2(20f, 20f);

    [Header("Follow Settings (跟随玩家设置)")]
    [Tooltip("是否处于跟随玩家模式（非建造模式下为 true，建造模式下为 false）")]
    public bool followPlayer = true;
    [Tooltip("跟随目标（通常为玩家）")]
    public Transform targetToFollow;
    [Tooltip("跟随偏移量")]
    public Vector3 followOffset = new Vector3(0f, 0f, -10f);

    [Header("Mobile Portrait Adaptation (手机竖屏适配设置)")]
    [Tooltip("是否启用竖屏底部功能UI偏置（使角色始终居中于屏幕上方 2/3 可视区域）")]
    public bool enableMobilePortraitBias = true;
    [Tooltip("底部功能UI占屏幕高度比例（默认 0.22f）")]
    [Range(0f, 0.5f)]
    public float bottomUIDockRatio = 0.22f;

    private Camera cam;
    private float targetZoom;
    private float zoomVelocity;

    private Vector3 targetPosition;
    private Vector3 panVelocity;

    // 鼠标状态
    private Vector2 lastMousePosition;
    private bool isMouseDragging = false;
    private bool isDragBlockedByUI = false;

    // 触控状态
    private Vector2 lastTouchPosition;
    private bool isTouchDragging = false;
    private float lastPinchDistance = 0f;
    private bool isPinching = false;
    private bool isTouchBlockedByUI;

    public static CameraController Instance { get; private set; }
    private Coroutine shakeCoroutine;
    private Vector3 shakeOffset = Vector3.zero;

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        targetZoom = cam.orthographicSize;
        targetPosition = transform.position;

        if (GetComponent<GameJam.ScreenFX>() == null)
        {
            gameObject.AddComponent<GameJam.ScreenFX>();
        }
        if (GameJam.TimeManager.I == null)
        {
            var tm = new GameObject("TimeManager");
            tm.AddComponent<GameJam.TimeManager>();
        }

        if (targetToFollow == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) targetToFollow = p.transform;
        }
    }

    /// <summary>
    /// 设置是否跟随玩家（建造模式传入 false，正常/战斗模式传入 true）
    /// </summary>
    public void SetFollowPlayer(bool follow)
    {
        followPlayer = follow;
        isMouseDragging = false;
        isTouchDragging = false;
        if (follow && targetToFollow == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) targetToFollow = p.transform;
        }
    }

    void Update()
    {
        if (PlayerSessionData.isCraftingOpen || Time.timeScale <= 0.0001f)
        {
            return;
        }

        if (BossEncounterController.EncounterActive && EnemyController.CombatWorldPaused) return;

        // 跟随玩家模式：摄像机目标始终锁定并对准玩家
        if (followPlayer && targetToFollow != null)
        {
            float biasY = 0f;
            if (enableMobilePortraitBias && cam != null && Screen.height > Screen.width)
            {
                // 竖屏适配：将摄像机向下偏置，使角色位于屏幕上方 2/3 (即 1 - bottomUIDockRatio) 视野区域的中心
                float orthoSize = float.IsNaN(cam.orthographicSize) ? 5f : cam.orthographicSize;
                biasY = -(bottomUIDockRatio * 0.5f) * (orthoSize * 2f);
            }
            // Encounter-only headroom; default P0 scale/zoom and non-boss follow are unchanged.
            var boss = BossEncounterController.Instance ? BossEncounterController.Instance.Boss : null;
            if (boss && boss.Entity.IsAlive) biasY += boss.UnitScale * 1.8f;
            targetPosition = new Vector3(
                targetToFollow.position.x + followOffset.x,
                targetToFollow.position.y + followOffset.y + biasY,
                transform.position.z
            );
        }

        if (Input.touchCount > 0)
        {
            HandleTouchInput();
        }
        else
        {
            isPinching = false;
            isTouchDragging = false;
            isTouchBlockedByUI = false;
            HandleMouseInput();
        }

        ApplyCameraUpdates();
    }

    private void HandleTouchInput()
    {
        // 双指捏合缩放（任何模式下均支持）
        if (Input.touchCount >= 2)
        {
            isTouchDragging = false;
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            if (isTouchBlockedByUI || IsTouchOverUI(t0) || IsTouchOverUI(t1))
            {
                isTouchBlockedByUI = true;
                isPinching = false;
                return;
            }

            float currentDistance = Vector2.Distance(t0.position, t1.position);

            if (!isPinching)
            {
                isPinching = true;
                lastPinchDistance = currentDistance;
            }
            else
            {
                float delta = currentDistance - lastPinchDistance;
                float pixelHeight = cam.pixelHeight > 0 ? cam.pixelHeight : Screen.height;
                float zoomFactor = (delta / pixelHeight) * pinchSensitivity * targetZoom;
                targetZoom -= zoomFactor;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);

                lastPinchDistance = currentDistance;
            }
            return;
        }

        isPinching = false;

        // 跟随玩家模式下禁用单指拖拽视野，使滑动操作全权用于快速短滑翻滚！
        if (followPlayer)
        {
            isTouchDragging = false;
            return;
        }

        // 单指平移拖拽（仅在自由视角 / 建造模式下生效）
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            // 防 UI 穿透
            if (touch.phase == TouchPhase.Began)
            {
                isTouchBlockedByUI = IsTouchOverUI(touch);
                if (isTouchBlockedByUI)
                {
                    isTouchDragging = false;
                    return;
                }
            }

            if (isTouchBlockedByUI) return;

            // 重新锚定基准点，防跳变
            if (!isTouchDragging || touch.phase == TouchPhase.Began)
            {
                isTouchDragging = true;
                lastTouchPosition = touch.position;
            }
            else if ((touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) && isTouchDragging)
            {
                Vector2 currentTouchPos = touch.position;
                Vector2 pixelDelta = currentTouchPos - lastTouchPosition;

                float pixelHeight = cam.pixelHeight > 0 ? cam.pixelHeight : Screen.height;
                float unitsPerPixel = (cam.orthographicSize * 2f) / pixelHeight;

                targetPosition.x -= pixelDelta.x * unitsPerPixel;
                targetPosition.y -= pixelDelta.y * unitsPerPixel;

                lastTouchPosition = currentTouchPos;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isTouchDragging = false;
            }
        }
        else
        {
            isTouchDragging = false;
        }
    }

    private bool IsTouchOverUI(Touch touch)
    {
        return (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId)) ||
               (BuildDockUI.Instance != null && BuildDockUI.Instance.IsPointerOverDrawer(touch.position));
    }

    private void HandleMouseInput()
    {
        // 鼠标滚轮缩放（任何模式下均支持）
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            float zoomDelta = scroll * mouseScrollSpeed * (targetZoom * 0.15f);
            targetZoom -= zoomDelta;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        // 跟随玩家模式下禁用鼠标拖拽平移，避免与短滑翻滚手势冲突！
        if (followPlayer)
        {
            isMouseDragging = false;
            return;
        }

        // 鼠标拖拽平移（仅在自由视角 / 建造模式下生效）
        if (Input.GetMouseButtonDown(mouseDragButton))
        {
            bool overUI = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                          (BuildDockUI.Instance != null && BuildDockUI.Instance.IsPointerOverDrawer(Input.mousePosition));

            if (overUI)
            {
                isDragBlockedByUI = true;
                isMouseDragging = false;
                return;
            }

            isDragBlockedByUI = false;
            isMouseDragging = true;
            lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(mouseDragButton))
        {
            if (isDragBlockedByUI || !isMouseDragging)
            {
                isMouseDragging = false;
                return;
            }

            Vector2 currentMousePos = Input.mousePosition;
            Vector2 pixelDelta = currentMousePos - lastMousePosition;

            float pixelHeight = cam.pixelHeight > 0 ? cam.pixelHeight : Screen.height;
            float unitsPerPixel = (cam.orthographicSize * 2f) / pixelHeight;

            targetPosition.x -= pixelDelta.x * unitsPerPixel;
            targetPosition.y -= pixelDelta.y * unitsPerPixel;

            lastMousePosition = currentMousePos;
        }
        else
        {
            isMouseDragging = false;
            isDragBlockedByUI = false;
        }
    }

    private void ApplyCameraUpdates()
    {
        if (Time.timeScale <= 0.0001f || PlayerSessionData.isCraftingOpen)
        {
            return;
        }

        // 严格防 NaN 保护
        if (float.IsNaN(targetPosition.x) || float.IsNaN(targetPosition.y) || float.IsNaN(targetPosition.z))
        {
            targetPosition = new Vector3(
                float.IsNaN(transform.position.x) ? 0f : transform.position.x,
                float.IsNaN(transform.position.y) ? 0f : transform.position.y,
                -10f
            );
            panVelocity = Vector3.zero;
        }

        if (float.IsNaN(targetZoom) || float.IsNaN(cam.orthographicSize))
        {
            targetZoom = 5f;
            cam.orthographicSize = 5f;
            zoomVelocity = 0f;
        }

        if (float.IsNaN(transform.position.x) || float.IsNaN(transform.position.y) || float.IsNaN(transform.position.z))
        {
            transform.position = new Vector3(targetPosition.x, targetPosition.y, -10f);
            panVelocity = Vector3.zero;
        }

        // 边界限制
        if (useBounds)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);
        }

        // 阻尼过渡
        if (panSmoothTime > 0.001f)
        {
            Vector3 newPos = Vector3.SmoothDamp(transform.position - shakeOffset, targetPosition, ref panVelocity, panSmoothTime);
            if (!float.IsNaN(newPos.x) && !float.IsNaN(newPos.y) && !float.IsNaN(newPos.z))
            {
                transform.position = newPos + shakeOffset;
            }
        }
        else
        {
            transform.position = targetPosition + shakeOffset;
        }

        if (zoomSmoothTime > 0.001f)
        {
            float newZoom = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
            if (!float.IsNaN(newZoom))
            {
                cam.orthographicSize = newZoom;
            }
        }
        else
        {
            cam.orthographicSize = targetZoom;
        }
    }

    /// <summary>
    /// 震屏特效 (支持真实时间)
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(DoShake(duration, magnitude));
    }

    private System.Collections.IEnumerator DoShake(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / Mathf.Max(duration, 0.001f);
            float damper = 1f - Mathf.Clamp01(t);
            Vector2 r = Random.insideUnitCircle * (magnitude * damper);
            shakeOffset = new Vector3(r.x, r.y, 0f);
            yield return null;
        }
        shakeOffset = Vector3.zero;
        shakeCoroutine = null;
    }

    /// <summary>
    /// 供外部聚焦定位
    /// </summary>
    public void FocusOn(Vector2 worldPosition, float zoom = -1f)
    {
        targetPosition = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        if (zoom > 0)
        {
            targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        }
    }
}

/// <summary>
/// 兼容旧版与 gamejam-integration 的全局震屏门面
/// </summary>
public static class CameraRig
{
    public static CameraController I => CameraController.Instance;
}
