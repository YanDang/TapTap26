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

    // 触控状态
    private Vector2 lastTouchPosition;
    private bool isTouchDragging = false;
    private float lastPinchDistance = 0f;
    private bool isPinching = false;

    void Awake()
    {
        cam = GetComponent<Camera>();
        targetZoom = cam.orthographicSize;
        targetPosition = transform.position;

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
        // 跟随玩家模式：摄像机目标始终锁定并对准玩家
        if (followPlayer && targetToFollow != null)
        {
            float biasY = 0f;
            if (enableMobilePortraitBias && cam != null)
            {
                // 竖屏适配：将摄像机向下偏置，使角色位于屏幕上方 2/3 (即 1 - bottomUIDockRatio) 视野区域的中心
                biasY = -(bottomUIDockRatio * 0.5f) * (cam.orthographicSize * 2f);
            }
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
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    isTouchDragging = false;
                    return;
                }
            }

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
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                isMouseDragging = false;
                return;
            }
            isMouseDragging = true;
            lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(mouseDragButton))
        {
            if (!isMouseDragging)
            {
                isMouseDragging = true;
                lastMousePosition = Input.mousePosition;
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
        }
    }

    private void ApplyCameraUpdates()
    {
        // 边界限制
        if (useBounds)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);
        }

        // 阻尼过渡
        if (panSmoothTime > 0.001f)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref panVelocity, panSmoothTime);
        }
        else
        {
            transform.position = targetPosition;
        }

        if (zoomSmoothTime > 0.001f)
        {
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
        }
        else
        {
            cam.orthographicSize = targetZoom;
        }
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
