using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace GameJam
{
    /// <summary>
    /// 移动端长按/短按动作按钮监听器（支持普通攻击点按与长按蓄力重砸）
    /// </summary>
    public class MobileAttackButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public System.Action onDown;
        public System.Action onUp;
        public System.Action onHold;
        public System.Action onCancel;
        public float holdDelay = 0.20f;

        private bool isDown = false;
        private float downTime = 0f;
        private bool holdTriggered = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            isDown = true;
            downTime = Time.unscaledTime;
            holdTriggered = false;
            onDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isDown) return;
            isDown = false;
            onUp?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isDown)
            {
                isDown = false;
                onCancel?.Invoke();
            }
        }

        void Update()
        {
            if (isDown && !holdTriggered)
            {
                if (Time.unscaledTime - downTime >= holdDelay)
                {
                    holdTriggered = true;
                    onHold?.Invoke();
                }
            }
        }
    }

    /// <summary>
    /// 移动端虚拟摇杆组件（支持 360° 平滑无极滑动、中心死区与松手自动回弹复位）
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform baseRect;
        public RectTransform knobRect;
        public float handleRange = 85f;
        public float deadZone = 12f;
        public Vector2 inputVector = Vector2.zero;

        private bool isTouching = false;
        private Canvas _canvas;

        void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (PlayerSessionData.CurrentMobileControlMode != PlayerSessionData.MobileControlMode.Joystick) return;
            isTouching = true;
            UpdateJoystick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isTouching) return;
            if (PlayerSessionData.CurrentMobileControlMode != PlayerSessionData.MobileControlMode.Joystick) return;
            UpdateJoystick(eventData);
        }

        private void UpdateJoystick(PointerEventData eventData)
        {
            if (baseRect == null || knobRect == null) return;

            Camera cam = (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : eventData.pressEventCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, eventData.position, cam, out Vector2 localPos))
            {
                float dist = localPos.magnitude;
                if (dist > handleRange)
                {
                    localPos = localPos.normalized * handleRange;
                }
                knobRect.anchoredPosition = localPos;

                if (dist > deadZone)
                {
                    float factor = Mathf.Clamp01((dist - deadZone) / (handleRange - deadZone));
                    inputVector = localPos.normalized * factor;
                }
                else
                {
                    inputVector = Vector2.zero;
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isTouching = false;
            inputVector = Vector2.zero;
            if (knobRect != null) knobRect.anchoredPosition = Vector2.zero;
            var p = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
            if (p != null) p.StopMovement();
        }

        void Update()
        {
            if (PlayerSessionData.CurrentMobileControlMode != PlayerSessionData.MobileControlMode.Joystick) return;
            if (inputVector.sqrMagnitude > 0.001f)
            {
                var p = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
                if (p != null)
                {
                    p.MoveWithInput(inputVector);
                }
            }
        }

        void OnDisable()
        {
            isTouching = false;
            inputVector = Vector2.zero;
            if (knobRect != null) knobRect.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>
    /// 全局 UI 战斗与状态指示 HUD：
    /// 1. 左上角状态栏：闪避 (Dodge) & 格挡 (Parry) 图标 + CD倒计时 + 战斗状态提示 + 背包/工坊入口
    /// 2. 右上角：天数与休息面板 + 【⚙️ 操作设置】按键（自由切换点击移动 / 虚拟摇杆）
    /// 3. 左下角：虚拟摇杆 (Virtual Joystick)，在摇杆模式下开启，360°平滑移动
    /// 4. 右下角移动端触控盘 (Action Pad)：
    ///    - 攻击/蓄力重砸大按键 (Attack)：点按近战挥砍/处决，按住超过0.2s开始蓄力，松开顺势冲刺震地重砸！
    ///    - 闪避翻滚按键 (Dodge Roll)：一键朝当前朝向闪避翻滚，附带冷却倒计时与出拳反击；
    ///    - 贝壳格挡按键 (Parry / Guard)：一键触发完美弹刀与缩壳防御；
    ///    - 踢推家具按键 (Push / Kick)：身边有家具时高亮，一键踢出家具飞撞敌人！
    /// 5. 自动全屏自适应 Canvas 架构 (1920x1080 0.5 Match)，代码自建无需手动拖入场景
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public static HUD I { get; private set; }
        public static HUD Instance => I;

        private Font _font;
        private Image _dodge;
        private Image _dodgeCd;
        private Image _parry;
        private Image _parryCd;
        private Text _dodgeTxt;
        private Text _parryTxt;
        private Text _combatTxt;
        private Text _dodgeLabel;
        private Text _parryLabel;
        private Button _restButton;
        private Text _dayText;
        private Text _restStatus;
        private Text _restButtonText;
        private float _restMessageUntil;
        private Button _craftingButton;
        private CombatSceneCraftingEntrance _craftingEntrance;

        // 移动端右下角虚拟操作按键群
        private RectTransform _actionPad;
        private Image _attackChargeGauge;
        private Image _dodgePadCd;
        private Text _dodgePadTxt;
        private Image _parryPadCd;
        private Text _parryPadTxt;
        private Button _pushBtn;
        private Image _pushBtnBg;

        // 移动端虚拟摇杆
        private VirtualJoystick _virtualJoystick;

        // 移动端操作设置按键与弹窗
        private Button _settingsButton;
        private RectTransform _settingsModal;
        private Image _modeTapBg;
        private Image _modeJoyBg;
        private Text _modeTapBadge;
        private Text _modeJoyBadge;

        public static HUD Build()
        {
            if (I != null) return I;

            var go = new GameObject("HUD", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1001;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            var h = go.AddComponent<HUD>();
            h.Create();
            return h;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            if (I == null && Object.FindObjectOfType<PlayerController>() != null)
            {
                Build();
            }
        }

        void Awake()
        {
            if (I == null)
            {
                I = this;
            }
            else if (I != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            PlayerSessionData.OnControlModeChanged -= HandleControlModeChanged;
        }

        public void Create()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 1. 左上角闪避图标 (位置 28, -28，尺寸 104x104)
            _dodge = Icon("Dodge", "ui_icon_dodge", new Vector2(28, -28), 104, out _dodgeCd);
            _dodgeTxt = Label(_dodge.transform, "", 34, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(104, 104));
            _dodgeLabel = Label(_dodge.transform, "闪避", 20, TextAnchor.UpperCenter, new Vector2(0, -60), new Vector2(104, 30));
            var dodgeBtn = _dodge.gameObject.AddComponent<Button>();
            dodgeBtn.onClick.AddListener(() => PlayerController.Instance?.TriggerDodgeRoll());
            _dodge.raycastTarget = true;

            // 2. 左上角格挡图标 (位置 146, -28，尺寸 104x104)
            _parry = Icon("Parry", "ui_icon_parry", new Vector2(146, -28), 104, out _parryCd);
            _parryTxt = Label(_parry.transform, "", 34, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(104, 104));
            _parryLabel = Label(_parry.transform, "格挡", 20, TextAnchor.UpperCenter, new Vector2(0, -60), new Vector2(104, 30));
            var parryBtn = _parry.gameObject.AddComponent<Button>();
            parryBtn.onClick.AddListener(() => PlayerController.Instance?.TriggerShellGuard());
            _parry.raycastTarget = true;

            // 3. 战斗中状态文字 (位置 266, -64，尺寸 300x40)
            _combatTxt = Label(transform, "", 26, TextAnchor.UpperLeft, Vector2.zero, new Vector2(300, 40));
            Anchor(_combatTxt.rectTransform, new Vector2(266, -64));
            _combatTxt.color = new Color(1f, 0.45f, 0.4f);

            // 4. 右上角休息/天数面板
            var restPanel = NewRect("RestPanel", transform);
            restPanel.anchorMin = restPanel.anchorMax = restPanel.pivot = new Vector2(1, 1);
            restPanel.anchoredPosition = new Vector2(-28, -28);
            restPanel.sizeDelta = new Vector2(360, 160);
            _dayText = Label(restPanel, "", 21, TextAnchor.MiddleCenter, new Vector2(0, 60), new Vector2(360, 36));
            var restRect = NewRect("BtnRestUntilNextDay", restPanel);
            restRect.sizeDelta = new Vector2(300, 54);
            var background = restRect.gameObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.30f, 0.27f, 0.95f);
            _restButton = restRect.gameObject.AddComponent<Button>();
            _restButton.targetGraphic = background;
            _restButton.onClick.AddListener(RestUntilNextDay);
            _restButtonText = Label(restRect, "休息到下一天", 24, TextAnchor.MiddleCenter, Vector2.zero, restRect.sizeDelta);
            _restStatus = Label(restPanel, "", 18, TextAnchor.MiddleCenter, new Vector2(0, -55), new Vector2(340, 48));

            // 5. 左侧背包/工坊入口
            _craftingEntrance = FindObjectOfType<CombatSceneCraftingEntrance>();
            var craftingRect = NewRect("BtnOpenCrafting", transform);
            Anchor(craftingRect, new Vector2(28, -205));
            craftingRect.sizeDelta = new Vector2(240, 88);
            var craftingBackground = craftingRect.gameObject.AddComponent<Image>();
            craftingBackground.color = new Color(0.12f, 0.30f, 0.27f, 0.95f);
            _craftingButton = craftingRect.gameObject.AddComponent<Button>();
            _craftingButton.targetGraphic = craftingBackground;
            _craftingButton.interactable = _craftingEntrance != null && _craftingEntrance.CanEnterCrafting;
            _craftingButton.onClick.AddListener(OpenCrafting);
            Label(craftingRect, "背包 / 合成", 28, TextAnchor.MiddleCenter, Vector2.zero, craftingRect.sizeDelta);

            // 6. 右上角操作设置按键 (位于每日精力面板正下方，纵向对齐，避开精力遮挡)
            var settingsRect = NewRect("BtnSettings", transform);
            settingsRect.anchorMin = settingsRect.anchorMax = settingsRect.pivot = new Vector2(1, 1);
            settingsRect.anchoredPosition = new Vector2(-28, -325);
            settingsRect.sizeDelta = new Vector2(220, 52);
            var settingsBg = settingsRect.gameObject.AddComponent<Image>();
            settingsBg.color = new Color(0.12f, 0.30f, 0.27f, 0.95f);
            _settingsButton = settingsRect.gameObject.AddComponent<Button>();
            _settingsButton.targetGraphic = settingsBg;
            _settingsButton.onClick.AddListener(OpenSettingsModal);

            var settingsBorder = Img("Border", "ui_ring", settingsRect, new Vector2(220, 52), new Color(0.25f, 0.85f, 0.80f, 0.8f));
            settingsBorder.rectTransform.anchorMin = settingsBorder.rectTransform.anchorMax = settingsBorder.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            settingsBorder.rectTransform.anchoredPosition = Vector2.zero;

            Label(settingsRect, "⚙️ 操作设置", 22, TextAnchor.MiddleCenter, Vector2.zero, settingsRect.sizeDelta);

            // 7. 右下角移动端虚拟动作盘 (Action Pad)
            CreateMobileActionPad();

            // 8. 左下角虚拟摇杆 (Virtual Joystick)
            CreateVirtualJoystick();

            // 9. 操作模式设置全屏弹窗 (Settings Modal)
            CreateSettingsModal();

            // 监听控制模式改变事件
            PlayerSessionData.OnControlModeChanged += HandleControlModeChanged;
            HandleControlModeChanged(PlayerSessionData.CurrentMobileControlMode);
        }

        private void CreateMobileActionPad()
        {
            _actionPad = NewRect("MobileActionPad", transform);
            _actionPad.anchorMin = _actionPad.anchorMax = _actionPad.pivot = new Vector2(1, 0);
            _actionPad.anchoredPosition = Vector2.zero;
            _actionPad.sizeDelta = new Vector2(440, 440);

            // A. 普通攻击 / 蓄力重砸大按键 (Attack)
            var attackRect = NewRect("BtnAttack", _actionPad);
            attackRect.anchorMin = attackRect.anchorMax = attackRect.pivot = new Vector2(1, 0);
            attackRect.anchoredPosition = new Vector2(-125, 125);
            attackRect.sizeDelta = new Vector2(132, 132);

            var attackBg = attackRect.gameObject.AddComponent<Image>();
            attackBg.sprite = Resources.Load<Sprite>("UI/ui_circle");
            attackBg.color = new Color(0.86f, 0.28f, 0.20f, 0.95f);
            attackBg.raycastTarget = true;

            var attackRing = Img("AttackRing", "ui_ring", attackRect, new Vector2(142, 142), new Color(1f, 1f, 1f, 0.5f));
            attackRing.rectTransform.anchorMin = attackRing.rectTransform.anchorMax = attackRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            attackRing.rectTransform.anchoredPosition = Vector2.zero;

            _attackChargeGauge = Img("AttackChargeGauge", "ui_circle", attackRect, new Vector2(132, 132), new Color(1f, 0.88f, 0.25f, 0.92f));
            _attackChargeGauge.type = Image.Type.Filled;
            _attackChargeGauge.fillMethod = Image.FillMethod.Radial360;
            _attackChargeGauge.fillOrigin = (int)Image.Origin360.Top;
            _attackChargeGauge.fillClockwise = true;
            _attackChargeGauge.fillAmount = 0f;
            _attackChargeGauge.rectTransform.anchorMin = _attackChargeGauge.rectTransform.anchorMax = _attackChargeGauge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _attackChargeGauge.rectTransform.anchoredPosition = Vector2.zero;

            var attackTxt = Label(attackRect, "斩", 42, TextAnchor.MiddleCenter, new Vector2(0, 8), new Vector2(120, 60));
            var attackSub = Label(attackRect, "按住蓄力", 16, TextAnchor.MiddleCenter, new Vector2(0, -32), new Vector2(120, 24));
            attackSub.color = new Color(1f, 0.95f, 0.8f, 0.9f);

            var attackBtnHandler = attackRect.gameObject.AddComponent<MobileAttackButton>();
            attackBtnHandler.onHold = () =>
            {
                var p = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
                if (p != null) p.StartCharge();
            };
            attackBtnHandler.onUp = () =>
            {
                var p = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
                if (p != null)
                {
                    if (p.IsCharging) p.ReleaseCharge();
                    else p.TriggerManualAttack();
                }
            };
            attackBtnHandler.onCancel = () =>
            {
                var p = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
                if (p != null && p.IsCharging) p.CancelCharge();
            };

            // B. 翻滚闪避按键 (Dodge Roll)
            var dodgeRect = NewRect("BtnDodge", _actionPad);
            dodgeRect.anchorMin = dodgeRect.anchorMax = dodgeRect.pivot = new Vector2(1, 0);
            dodgeRect.anchoredPosition = new Vector2(-260, 95);
            dodgeRect.sizeDelta = new Vector2(98, 98);

            var dodgeBg = dodgeRect.gameObject.AddComponent<Image>();
            dodgeBg.sprite = Resources.Load<Sprite>("UI/ui_circle");
            dodgeBg.color = new Color(0.18f, 0.38f, 0.44f, 0.95f);
            dodgeBg.raycastTarget = true;

            var dodgeBtnComp = dodgeRect.gameObject.AddComponent<Button>();
            dodgeBtnComp.targetGraphic = dodgeBg;
            dodgeBtnComp.onClick.AddListener(() =>
            {
                var p = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
                if (p != null) p.TriggerDodgeRoll();
            });

            var dodgeIcon = Img("DodgeIcon", "ui_icon_dodge", dodgeRect, new Vector2(62, 62), Color.white);
            dodgeIcon.rectTransform.anchorMin = dodgeIcon.rectTransform.anchorMax = dodgeIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            dodgeIcon.rectTransform.anchoredPosition = new Vector2(0, 10);

            var dodgeLabel = Label(dodgeRect, "闪避", 18, TextAnchor.MiddleCenter, new Vector2(0, -32), new Vector2(90, 24));

            _dodgePadCd = Img("DodgePadCD", "ui_circle", dodgeRect, new Vector2(90, 90), new Color(0, 0, 0, 0.65f));
            _dodgePadCd.type = Image.Type.Filled;
            _dodgePadCd.fillMethod = Image.FillMethod.Radial360;
            _dodgePadCd.fillOrigin = (int)Image.Origin360.Top;
            _dodgePadCd.fillClockwise = false;
            _dodgePadCd.fillAmount = 0;
            _dodgePadCd.rectTransform.anchorMin = _dodgePadCd.rectTransform.anchorMax = _dodgePadCd.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _dodgePadCd.rectTransform.anchoredPosition = Vector2.zero;

            _dodgePadTxt = Label(dodgeRect, "", 32, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(98, 98));

            // C. 贝壳防御 / 完美弹刀按键 (Parry / Guard)
            var parryRect = NewRect("BtnParry", _actionPad);
            parryRect.anchorMin = parryRect.anchorMax = parryRect.pivot = new Vector2(1, 0);
            parryRect.anchoredPosition = new Vector2(-105, 260);
            parryRect.sizeDelta = new Vector2(98, 98);

            var parryBg = parryRect.gameObject.AddComponent<Image>();
            parryBg.sprite = Resources.Load<Sprite>("UI/ui_circle");
            parryBg.color = new Color(0.16f, 0.32f, 0.50f, 0.95f);
            parryBg.raycastTarget = true;

            var parryBtnComp = parryRect.gameObject.AddComponent<Button>();
            parryBtnComp.targetGraphic = parryBg;
            parryBtnComp.onClick.AddListener(() =>
            {
                var p = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
                if (p != null) p.TriggerShellGuard();
            });

            var parryIcon = Img("ParryIcon", "ui_icon_parry", parryRect, new Vector2(62, 62), Color.white);
            parryIcon.rectTransform.anchorMin = parryIcon.rectTransform.anchorMax = parryIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            parryIcon.rectTransform.anchoredPosition = new Vector2(0, 10);

            var parryLabel = Label(parryRect, "格挡", 18, TextAnchor.MiddleCenter, new Vector2(0, -32), new Vector2(90, 24));

            _parryPadCd = Img("ParryPadCD", "ui_circle", parryRect, new Vector2(90, 90), new Color(0, 0, 0, 0.65f));
            _parryPadCd.type = Image.Type.Filled;
            _parryPadCd.fillMethod = Image.FillMethod.Radial360;
            _parryPadCd.fillOrigin = (int)Image.Origin360.Top;
            _parryPadCd.fillClockwise = false;
            _parryPadCd.fillAmount = 0;
            _parryPadCd.rectTransform.anchorMin = _parryPadCd.rectTransform.anchorMax = _parryPadCd.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _parryPadCd.rectTransform.anchoredPosition = Vector2.zero;

            _parryPadTxt = Label(parryRect, "", 32, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(98, 98));

            // D. 踢推身边家具按键 (Push / Kick Furniture)
            var pushRect = NewRect("BtnPushFurniture", _actionPad);
            pushRect.anchorMin = pushRect.anchorMax = pushRect.pivot = new Vector2(1, 0);
            pushRect.anchoredPosition = new Vector2(-235, 220);
            pushRect.sizeDelta = new Vector2(86, 86);

            _pushBtnBg = pushRect.gameObject.AddComponent<Image>();
            _pushBtnBg.sprite = Resources.Load<Sprite>("UI/ui_circle");
            _pushBtnBg.color = new Color(0.85f, 0.52f, 0.15f, 0.95f);
            _pushBtnBg.raycastTarget = true;

            _pushBtn = pushRect.gameObject.AddComponent<Button>();
            _pushBtn.targetGraphic = _pushBtnBg;
            _pushBtn.onClick.AddListener(() =>
            {
                var p = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
                if (p != null) p.KickNearbyFurniture();
            });

            Label(pushRect, "推撞", 22, TextAnchor.MiddleCenter, new Vector2(0, 8), new Vector2(80, 40));
            var pushSub = Label(pushRect, "家具", 15, TextAnchor.MiddleCenter, new Vector2(0, -22), new Vector2(80, 25));
            pushSub.color = new Color(1f, 0.95f, 0.8f, 0.9f);
        }

        private void CreateVirtualJoystick()
        {
            var joyRoot = NewRect("VirtualJoystickRoot", transform);
            joyRoot.anchorMin = joyRoot.anchorMax = new Vector2(0, 0);
            joyRoot.pivot = new Vector2(0.5f, 0.5f);
            joyRoot.anchoredPosition = new Vector2(240, 240);
            joyRoot.sizeDelta = new Vector2(240, 240);

            // 摇杆底座圆盘
            var baseImage = joyRoot.gameObject.AddComponent<Image>();
            baseImage.sprite = Resources.Load<Sprite>("UI/ui_circle");
            baseImage.color = new Color(0.10f, 0.22f, 0.26f, 0.65f);
            baseImage.raycastTarget = true;

            // 底座生体霓虹光环
            var ring = Img("JoystickRing", "ui_ring", joyRoot, new Vector2(236, 236), new Color(0.20f, 0.85f, 0.80f, 0.65f));
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ring.rectTransform.anchoredPosition = Vector2.zero;

            // 摇杆操纵滑块 (Knob)
            var knobRect = NewRect("JoystickKnob", joyRoot);
            knobRect.anchorMin = knobRect.anchorMax = knobRect.pivot = new Vector2(0.5f, 0.5f);
            knobRect.anchoredPosition = Vector2.zero;
            knobRect.sizeDelta = new Vector2(88, 88);

            var knobImage = knobRect.gameObject.AddComponent<Image>();
            knobImage.sprite = Resources.Load<Sprite>("UI/ui_circle");
            knobImage.color = new Color(0.22f, 0.82f, 0.84f, 0.95f);
            knobImage.raycastTarget = false;

            var knobRing = Img("KnobRing", "ui_ring", knobRect, new Vector2(92, 92), new Color(1f, 1f, 1f, 0.85f));
            knobRing.rectTransform.anchorMin = knobRing.rectTransform.anchorMax = knobRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            knobRing.rectTransform.anchoredPosition = Vector2.zero;

            var knobCenterDot = Img("KnobDot", "ui_circle", knobRect, new Vector2(24, 24), Color.white);
            knobCenterDot.rectTransform.anchorMin = knobCenterDot.rectTransform.anchorMax = knobCenterDot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            knobCenterDot.rectTransform.anchoredPosition = Vector2.zero;

            _virtualJoystick = joyRoot.gameObject.AddComponent<VirtualJoystick>();
            _virtualJoystick.baseRect = joyRoot;
            _virtualJoystick.knobRect = knobRect;
            _virtualJoystick.handleRange = 85f;
            _virtualJoystick.deadZone = 12f;
        }

        private void CreateSettingsModal()
        {
            _settingsModal = NewRect("SettingsModal", transform);
            _settingsModal.anchorMin = Vector2.zero;
            _settingsModal.anchorMax = Vector2.one;
            _settingsModal.pivot = new Vector2(0.5f, 0.5f);
            _settingsModal.offsetMin = _settingsModal.offsetMax = Vector2.zero;

            var modalCanvas = _settingsModal.gameObject.AddComponent<Canvas>();
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingOrder = 3000;
            _settingsModal.gameObject.AddComponent<GraphicRaycaster>();

            // 半透明全屏遮罩，阻止穿透点击
            var maskImg = _settingsModal.gameObject.AddComponent<Image>();
            maskImg.color = new Color(0f, 0f, 0f, 0.72f);
            maskImg.raycastTarget = true;

            // 居中设置面板容器
            var dialog = NewRect("Dialog", _settingsModal);
            dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(0.5f, 0.5f);
            dialog.anchoredPosition = Vector2.zero;
            dialog.sizeDelta = new Vector2(650, 280);

            var dialogBg = dialog.gameObject.AddComponent<Image>();
            dialogBg.color = new Color(0.08f, 0.16f, 0.19f, 0.98f);
            dialogBg.raycastTarget = true;

            var outline = dialog.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.85f, 0.80f, 0.95f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            // 标题
            Label(dialog, "🎮 移动控制设置 (Control Settings)", 25, TextAnchor.MiddleCenter, new Vector2(0, 95), new Vector2(600, 40));
            var subTitle = Label(dialog, "选择您在手机端的移动控制方式：", 17, TextAnchor.MiddleCenter, new Vector2(0, 56), new Vector2(560, 28));
            subTitle.color = new Color(0.7f, 0.95f, 0.95f);

            // 模式选项 1：点击寻路移动
            var tapRect = NewRect("BtnModeTap", dialog);
            tapRect.anchorMin = tapRect.anchorMax = tapRect.pivot = new Vector2(0.5f, 0.5f);
            tapRect.anchoredPosition = new Vector2(-150, 0);
            tapRect.sizeDelta = new Vector2(260, 72);

            _modeTapBg = tapRect.gameObject.AddComponent<Image>();
            _modeTapBg.color = new Color(0.14f, 0.45f, 0.40f, 0.95f);
            _modeTapBg.raycastTarget = true;

            var btnTapComp = tapRect.gameObject.AddComponent<Button>();
            btnTapComp.targetGraphic = _modeTapBg;
            btnTapComp.onClick.AddListener(() =>
            {
                PlayerSessionData.CurrentMobileControlMode = PlayerSessionData.MobileControlMode.TapToMove;
                RefreshSettingsModal();
            });

            Label(tapRect, "👆 点击寻路移动", 22, TextAnchor.MiddleCenter, new Vector2(0, 12), new Vector2(250, 32));
            _modeTapBadge = Label(tapRect, "【当前生效】", 17, TextAnchor.MiddleCenter, new Vector2(0, -15), new Vector2(200, 26));
            _modeTapBadge.color = Color.green;

            // 模式选项 2：虚拟摇杆移动
            var joyRect = NewRect("BtnModeJoystick", dialog);
            joyRect.anchorMin = joyRect.anchorMax = joyRect.pivot = new Vector2(0.5f, 0.5f);
            joyRect.anchoredPosition = new Vector2(150, 0);
            joyRect.sizeDelta = new Vector2(260, 72);

            _modeJoyBg = joyRect.gameObject.AddComponent<Image>();
            _modeJoyBg.color = new Color(0.14f, 0.25f, 0.28f, 0.85f);
            _modeJoyBg.raycastTarget = true;

            var btnJoyComp = joyRect.gameObject.AddComponent<Button>();
            btnJoyComp.targetGraphic = _modeJoyBg;
            btnJoyComp.onClick.AddListener(() =>
            {
                PlayerSessionData.CurrentMobileControlMode = PlayerSessionData.MobileControlMode.Joystick;
                RefreshSettingsModal();
            });

            Label(joyRect, "🕹️ 虚拟摇杆移动", 22, TextAnchor.MiddleCenter, new Vector2(0, 12), new Vector2(250, 32));
            _modeJoyBadge = Label(joyRect, "【点击切换】", 17, TextAnchor.MiddleCenter, new Vector2(0, -15), new Vector2(200, 26));
            _modeJoyBadge.color = Color.gray;

            // 确定 / 保存按钮
            var confirmRect = NewRect("BtnConfirm", dialog);
            confirmRect.anchorMin = confirmRect.anchorMax = confirmRect.pivot = new Vector2(0.5f, 0.5f);
            confirmRect.anchoredPosition = new Vector2(0, -88);
            confirmRect.sizeDelta = new Vector2(240, 46);

            var confirmBg = confirmRect.gameObject.AddComponent<Image>();
            confirmBg.color = new Color(0.18f, 0.55f, 0.48f, 0.98f);

            var confirmBtn = confirmRect.gameObject.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmBg;
            confirmBtn.onClick.AddListener(CloseSettingsModal);
            Label(confirmRect, "确定并保存", 20, TextAnchor.MiddleCenter, Vector2.zero, confirmRect.sizeDelta);

            // 右上角关闭按钮
            var closeRect = NewRect("BtnClose", dialog);
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1, 1);
            closeRect.anchoredPosition = new Vector2(-15, -15);
            closeRect.sizeDelta = new Vector2(44, 44);

            var closeBg = closeRect.gameObject.AddComponent<Image>();
            closeBg.color = new Color(0.35f, 0.15f, 0.15f, 0.95f);

            var closeBtn = closeRect.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(CloseSettingsModal);
            Label(closeRect, "×", 28, TextAnchor.MiddleCenter, Vector2.zero, closeRect.sizeDelta);

            // 默认关闭弹窗
            _settingsModal.gameObject.SetActive(false);
        }

        public void OpenSettingsModal()
        {
            if (_settingsModal != null)
            {
                _settingsModal.gameObject.SetActive(true);
                RefreshSettingsModal();
            }
        }

        public void CloseSettingsModal()
        {
            if (_settingsModal != null)
            {
                _settingsModal.gameObject.SetActive(false);
            }
        }

        private void RefreshSettingsModal()
        {
            bool isJoystick = PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.Joystick;

            if (_modeTapBg != null)
                _modeTapBg.color = !isJoystick ? new Color(0.18f, 0.55f, 0.48f, 0.98f) : new Color(0.14f, 0.25f, 0.28f, 0.85f);
            if (_modeJoyBg != null)
                _modeJoyBg.color = isJoystick ? new Color(0.18f, 0.55f, 0.48f, 0.98f) : new Color(0.14f, 0.25f, 0.28f, 0.85f);

            if (_modeTapBadge != null)
            {
                _modeTapBadge.text = !isJoystick ? "【当前生效中】" : "【点击切换】";
                _modeTapBadge.color = !isJoystick ? new Color(0.2f, 0.95f, 0.7f) : new Color(0.6f, 0.6f, 0.6f);
            }
            if (_modeJoyBadge != null)
            {
                _modeJoyBadge.text = isJoystick ? "【当前生效中】" : "【点击切换】";
                _modeJoyBadge.color = isJoystick ? new Color(0.2f, 0.95f, 0.7f) : new Color(0.6f, 0.6f, 0.6f);
            }
        }

        private void HandleControlModeChanged(PlayerSessionData.MobileControlMode mode)
        {
            bool isJoystick = mode == PlayerSessionData.MobileControlMode.Joystick;
            if (_virtualJoystick != null)
            {
                _virtualJoystick.gameObject.SetActive(isJoystick);
            }
            var player = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
            if (player != null)
            {
                player.StopMovement();
            }
        }

        private void OpenCrafting()
        {
            if (_craftingEntrance == null) _craftingEntrance = FindObjectOfType<CombatSceneCraftingEntrance>();
            if (_craftingEntrance != null) _craftingEntrance.EnterCraftingScene();
        }

        private void RestUntilNextDay()
        {
            var player = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
            PlayerSessionData.TryRestUntilNextDay(player, out var message);
            _restStatus.text = message;
            _restMessageUntil = Time.unscaledTime + 3f;
        }

        // ---------------------------------------------------------------- helpers
        static RectTransform NewRect(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Anchor(RectTransform r, Vector2 pos)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = pos;
        }

        Image Img(string n, string sprite, Transform parent, Vector2 size, Color c)
        {
            var r = NewRect(n, parent);
            var im = r.gameObject.AddComponent<Image>();
            im.sprite = Resources.Load<Sprite>("UI/" + sprite);
            im.color = c;
            im.raycastTarget = false;
            r.sizeDelta = size;
            return im;
        }

        Text Label(Transform parent, string s, int size, TextAnchor a, Vector2 pos, Vector2 box)
        {
            var r = NewRect("Text", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = a;
            t.text = s;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = r.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.8f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            r.sizeDelta = box;
            r.anchoredPosition = pos;
            return t;
        }

        Image Icon(string n, string sprite, Vector2 pos, float size, out Image cd)
        {
            var im = Img(n, sprite, transform, new Vector2(size, size), Color.white);
            Anchor(im.rectTransform, pos);
            cd = Img(n + "CD", "ui_circle", im.transform, new Vector2(size - 10, size - 10), new Color(0, 0, 0, 0.65f));
            cd.type = Image.Type.Filled;
            cd.fillMethod = Image.FillMethod.Radial360;
            cd.fillOrigin = (int)Image.Origin360.Top;
            cd.fillClockwise = false;
            cd.fillAmount = 0;
            return im;
        }

        // ---------------------------------------------------------------- update
        void Update()
        {
            // 在采集场景或合成工坊界面打开时，彻底隐藏战斗 HUD
            bool hideHud = PlayerSessionData.isCraftingOpen || 
                           UnityEngine.SceneManagement.SceneManager.GetSceneByName("Collect").isLoaded || 
                           UnityEngine.SceneManagement.SceneManager.GetSceneByName("Mix").isLoaded;

            SetVisible(!hideHud);
            if (hideHud) return;

            var p = PlayerController.Instance;
            if (p == null) p = FindObjectOfType<PlayerController>();
            if (!p)
            {
                SetVisible(false);
                return;
            }

            // 抽屉展开时，隐藏右下角虚拟操作盘与虚拟摇杆，防止与建造抽屉货架重叠
            bool isDrawerOpen = BuildDockUI.Instance != null && BuildDockUI.Instance.isDrawerOpen;
            if (_actionPad != null)
            {
                _actionPad.gameObject.SetActive(!isDrawerOpen);
            }
            if (_virtualJoystick != null)
            {
                bool isJoystickMode = PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.Joystick;
                _virtualJoystick.gameObject.SetActive(!isDrawerOpen && isJoystickMode);
            }

            if (_craftingButton != null)
            {
                if (_craftingEntrance == null) _craftingEntrance = FindObjectOfType<CombatSceneCraftingEntrance>();
                _craftingButton.interactable = _craftingEntrance != null && _craftingEntrance.CanEnterCrafting;
            }

            if (_restButton != null)
            {
                _dayText.text = $"第 {PlayerSessionData.greenhouse.day} / 7 天 · {PlayerSessionData.GetDangerLevelLabel(PlayerSessionData.greenhouse.day)}";
                _restButtonText.text = PlayerSessionData.greenhouse.day >= GameRunProgress.FinalDay ? "结束旅程 · 结算" : "休息到下一天";
                _restButton.interactable = PlayerSessionData.CanRestUntilNextDay(p, out var reason);
                if (Time.unscaledTime >= _restMessageUntil)
                    _restStatus.text = _restButton.interactable
                        ? (PlayerSessionData.greenhouse.day >= GameRunProgress.FinalDay ? "按当前有效家具数量结算" : "休息后需重新挑战采集门守卫")
                        : reason;
            }

            bool inCombat = p.InCombat;
            var grey = new Color(0.5f, 0.5f, 0.5f, 0.75f);

            // 脱战置灰，战斗中高亮
            if (_dodge != null) _dodge.color = inCombat ? Color.white : grey;
            if (_parry != null) _parry.color = inCombat ? Color.white : grey;

            // 闪避冷却扇形遮罩与剩余时间
            float dodgeRem = p.DodgeRemaining;
            float dodgeCd = Mathf.Max(p.dodgeCooldown, 0.01f);
            float dodgeRatio = Mathf.Clamp01(dodgeRem / dodgeCd);
            string dodgeStr = dodgeRem > 0.05f ? Mathf.CeilToInt(dodgeRem).ToString() : "";

            if (_dodgeCd != null) _dodgeCd.fillAmount = dodgeRatio;
            if (_dodgeTxt != null) _dodgeTxt.text = dodgeStr;
            if (_dodgePadCd != null) _dodgePadCd.fillAmount = dodgeRatio;
            if (_dodgePadTxt != null) _dodgePadTxt.text = dodgeStr;

            // 格挡冷却扇形遮罩与剩余时间
            float parryRem = p.ParryRemaining;
            float parryCd = Mathf.Max(p.parryCooldown, 0.01f);
            float parryRatio = Mathf.Clamp01(parryRem / parryCd);
            string parryStr = parryRem > 0.05f ? Mathf.CeilToInt(parryRem).ToString() : "";

            if (_parryCd != null) _parryCd.fillAmount = parryRatio;
            if (_parryTxt != null) _parryTxt.text = parryStr;
            if (_parryPadCd != null) _parryPadCd.fillAmount = parryRatio;
            if (_parryPadTxt != null) _parryPadTxt.text = parryStr;

            // 蓄力重砸充能进度
            if (_attackChargeGauge != null)
            {
                _attackChargeGauge.fillAmount = p.IsCharging ? p.ChargeLevel : 0f;
            }

            // 踢推家具按键动态可用状态
            if (_pushBtn != null)
            {
                var nearFurn = FurnitureObject.GetNearestPlaced(p.transform.position, 2.2f);
                bool canPush = nearFurn != null && !nearFurn.isImmovable && nearFurn.instanceId != "furn_gather_door" && nearFurn.GetComponent<GatherDoorAuraBehavior>() == null;
                _pushBtn.interactable = canPush;
                if (_pushBtnBg != null)
                {
                    _pushBtnBg.color = canPush ? new Color(0.85f, 0.52f, 0.15f, 0.95f) : new Color(0.35f, 0.35f, 0.35f, 0.45f);
                }
            }

            // 战斗状态提示
            if (_combatTxt != null)
            {
                _combatTxt.text = inCombat ? "战斗中" : "";
            }
        }

        /// <summary>
        /// 切换 HUD 显示状态（例如进入工坊界面时可暂时隐藏）
        /// </summary>
        public void SetVisible(bool visible)
        {
            var cv = GetComponent<Canvas>();
            if (cv != null) cv.enabled = visible;
        }
    }
}
