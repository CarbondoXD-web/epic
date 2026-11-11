using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第一人称漫游控制器：鼠标环视、WASD 行走、Shift 疾跑、空格跳跃。
/// F 在第一人称与全景观察之间切换；Esc 释放鼠标，点击画面重新锁定；
/// 在第一人称里按 R 回到出生点。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonWalker : MonoBehaviour
{
    [Header("移动")]
    [Tooltip("常规行走速度（米/秒）")]
    public float walkSpeed = 2.4f;
    [Tooltip("按住 Shift 时的疾跑速度（米/秒）")]
    public float sprintSpeed = 6.0f;
    [Tooltip("水平速度趋近目标值的快慢，越大越跟手")]
    public float acceleration = 16f;
    [Tooltip("空中操控力度，0 表示完全无法在空中改变方向")]
    [Range(0f, 1f)] public float airControl = 0.35f;
    [Tooltip("跳跃高度（米）")]
    public float jumpHeight = 1.1f;
    [Tooltip("重力加速度，负数")]
    public float gravity = -22f;

    [Header("视角")]
    [Tooltip("第一人称相机（留空则自动用 firstPersonCamera）")]
    public Transform head;
    [Tooltip("鼠标灵敏度：鼠标每移动 1 像素转多少度")]
    public float mouseSensitivity = 0.13f;
    [Range(-89f, 0f)] public float minPitch = -85f;
    [Range(0f, 89f)] public float maxPitch = 85f;

    [Header("与全景视图联动")]
    [Tooltip("第一人称相机，进入漫游时启用")]
    public Camera firstPersonCamera;
    [Tooltip("整线全景相机，进入漫游时停用（AudioListener 仍保留，声音不受影响）")]
    public Camera overviewCamera;
    [Tooltip("全景相机上的控制脚本（FactoryLayoutView）")]
    public Behaviour overviewControl;

    [Header("启动")]
    public bool startInFirstPerson = true;
    public bool lockCursorOnStart = true;

    const float CoyoteTime = 0.12f;      // 离开地面后仍可起跳的宽限
    const float JumpBufferTime = 0.12f;  // 落地前提前按跳的缓存

    CharacterController controller;
    Font font;

    Vector3 horizontalVelocity;
    float verticalVelocity;
    float yaw, pitch;
    float coyoteTimer, jumpBufferTimer;
    CollisionFlags lastFlags;
    bool firstPerson;

    Vector3 spawnPosition;
    float spawnYaw;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (head == null && firstPersonCamera != null) head = firstPersonCamera.transform;
        if (firstPersonCamera == null && head != null) firstPersonCamera = head.GetComponent<Camera>();

        yaw = transform.eulerAngles.y;
        if (head != null) pitch = NormalizePitch(head.localEulerAngles.x);

        spawnPosition = transform.position;
        spawnYaw = yaw;

        font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 20);
    }

    void Start()
    {
        SetFirstPerson(startInFirstPerson);
        if (startInFirstPerson && !lockCursorOnStart) UnlockCursor();
    }

    void OnDisable()
    {
        UnlockCursor();
    }

    void OnApplicationFocus(bool focused)
    {
        // 从别的窗口切回来时恢复锁定；切走时先释放，免得鼠标被“吃掉”
        if (!focused) UnlockCursor();
        else if (firstPerson) LockCursor();
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (kb != null)
        {
            if (kb.fKey.wasPressedThisFrame) SetFirstPerson(!firstPerson);
            if (kb.escapeKey.wasPressedThisFrame) UnlockCursor();
        }

        if (!firstPerson) return;

        // 鼠标被释放后，点击画面重新锁定（不会误触发场景里的 UI）
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            LockCursor();

        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            Look(mouse.delta.ReadValue());

        if (kb != null && kb.rKey.wasPressedThisFrame) Respawn();

        Move(kb);
    }

    // ---------------- 视角 ----------------

    void Look(Vector2 delta)
    {
        yaw += delta.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (head != null) head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // ---------------- 移动 ----------------

    void Move(Keyboard kb)
    {
        Vector2 input = Vector2.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
        }
        if (input.sqrMagnitude > 1f) input.Normalize();

        bool sprinting = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        float speed = sprinting ? sprintSpeed : walkSpeed;

        Vector3 wish = transform.right * input.x + transform.forward * input.y;
        Vector3 targetVelocity = wish * speed;

        bool onGround = controller.isGrounded || (lastFlags & CollisionFlags.Below) != 0;
        float accel = onGround ? acceleration : acceleration * airControl;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, accel * Time.deltaTime);

        // 起跳：落地宽限 + 按键缓存，避免“明明按了却没跳”
        float dt = Time.deltaTime;
        if (onGround)
        {
            coyoteTimer = CoyoteTime;
            if (verticalVelocity < 0f) verticalVelocity = -2f;   // 贴地，避免台阶上抖动
        }
        else
        {
            coyoteTimer -= dt;
        }

        if (kb != null && kb.spaceKey.wasPressedThisFrame) jumpBufferTimer = JumpBufferTime;
        else jumpBufferTimer -= dt;

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            onGround = false;
        }

        verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, -50f);

        Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
        lastFlags = controller.Move(motion * dt);

        // 顶到天花板就把上升速度清掉
        if ((lastFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
    }

    void Respawn()
    {
        controller.enabled = false;
        transform.position = spawnPosition;
        controller.enabled = true;
        yaw = spawnYaw;
        pitch = 0f;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (head != null) head.localRotation = Quaternion.identity;
    }

    // ---------------- 视图切换 ----------------

    public void SetFirstPerson(bool on)
    {
        firstPerson = on;

        if (firstPersonCamera != null) firstPersonCamera.enabled = on;
        if (overviewCamera != null) overviewCamera.enabled = !on;
        if (overviewControl != null) overviewControl.enabled = !on;

        // 让 Camera.main 始终指向正在渲染的那台相机：场景里的点击检测（DirectFactoryControls）
        // 用的是 Camera.main，只停用相机而不改标签会让它变成 null 并抛空引用。
        if (firstPersonCamera != null && overviewCamera != null &&
            firstPersonCamera.gameObject != overviewCamera.gameObject)
        {
            firstPersonCamera.gameObject.tag = on ? "MainCamera" : "Untagged";
            overviewCamera.gameObject.tag = on ? "Untagged" : "MainCamera";
        }

        if (on) LockCursor(); else UnlockCursor();
    }

    static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static float NormalizePitch(float angle)
    {
        angle %= 360f;
        return angle > 180f ? angle - 360f : angle;
    }

    // ---------------- 提示 ----------------

    void OnGUI()
    {
        if (!firstPerson) return;

        // 准星
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(cx - 3f, cy - 3f, 6f, 6f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 操作提示（左下角，跟随分辨率缩放）
        float scale = Screen.width / 1920f;
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        if (font != null) GUI.skin.font = font;

        float boxWidth = 720f;
        float boxHeight = 92f;
        float left = 20f;
        float top = Screen.height / scale - boxHeight - 20f;

        GUI.Box(new Rect(left, top, boxWidth, boxHeight), "");
        var label = new GUIStyle(GUI.skin.label) { fontSize = 20 };
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        GUI.Label(new Rect(left + 16f, top + 10f, boxWidth - 32f, 30f),
            "第一人称漫游 · WASD 行走 · Shift 疾跑 · 空格 跳跃", label);
        GUI.Label(new Rect(left + 16f, top + 44f, boxWidth - 32f, 30f),
            locked ? "鼠标 环视 · Esc 释放鼠标 · R 回出生点 · F 返回全景观察"
                   : "鼠标已释放 · 点击画面继续环视 · F 返回全景观察", label);

        GUI.matrix = previous;
    }
}
