using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using UnityEngine.XR;

using CommonUsages = UnityEngine.XR.CommonUsages;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class SimpleFPPController : MonoBehaviour
{
    public enum ControlMode { AutoDetect, ForceMobileTouch, ForceVR }

    [Header("Platform & Mode Settings")]
    public ControlMode controlMode = ControlMode.AutoDetect;
    public GameObject mobileUICanvas;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 1.5f;

    [Header("Input Smoothing & Snapping")]
    public float axisSnapThreshold = 0.15f;
    [Tooltip("Increase this if your player moves automatically due to joystick drift")]
    public float inputDeadzone = 0.2f;

    [Header("Collision (Physics-less)")]
    [Tooltip("Prevents walking through walls without needing a Rigidbody or Collider")]
    public bool enableWallCollision = true;
    public float playerRadius = 0.3f;
    public float playerHeight = 1.6f;
    public LayerMask wallCollisionLayer = ~0;

    [Header("Joystick Pack Integration")]
    public Joystick movementJoystick;
    public Joystick lookJoystick;

    [Header("Look / Rotation Settings")]
    public Transform playerCamera;
    public float mouseSensitivity = 0.5f;
    public float touchSensitivity = 0.012f; // Reduced slightly
    public float joystickLookSensitivity = 60f;
    public float keyboardTurnSpeed = 90f;
    public bool lockCursorOnDesktop = true;
    public float rotationSmoothTime = 0.08f; // For smooth and stable camera movement

    [Header("Pitch / Vertical Look Limits")]
    [Tooltip("Minimum vertical look angle in degrees (looking up, e.g. -80)")]
    public float minPitch = -80f;
    [Tooltip("Maximum vertical look angle in degrees (looking down, e.g. 80)")]
    public float maxPitch = 80f;
    [Tooltip("Invert vertical look axis")]
    public bool invertY = false;

    [Header("VR Settings")]
    public bool useVRSnapTurn = true;
    public float vrSnapTurnAngle = 45f;
    public float vrSnapTurnCooldown = 0.2f;

    [Header("VR Controller Anchors")]
    public Transform leftControllerAnchor;
    public Transform rightControllerAnchor;
    public bool vrHeadOrientedMovement = true;
    public bool autoCreateControllerAnchors = false;

    private float yaw;
    private float pitch;
    private float currentYaw;
    private float currentPitch;
    private float yawVelocity;
    private float pitchVelocity;
    private bool isVRActive;

    // Physics-less gravity & jump
    private float verticalVelocity = 0f;
    private const float gravity = -9.81f;

    // Spawn / Reset position
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Quaternion initialCameraRotation = Quaternion.identity;

    // State
    private Transform xrOriginRoot;
    private InputSystem_Actions inputActions;
    private bool prevVRJumpState = false;
    private bool uiJumpTriggered = false;
    private float snapTurnTimer = 0f;

    // Touch State
    private int lookTouchFingerId = -1;
    private Vector2 lastLookTouchPos;

    private void Awake()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        yaw = transform.eulerAngles.y;
        currentYaw = yaw;

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (playerCamera != null)
        {
            initialCameraRotation = playerCamera.localRotation;
            pitch = 0f;
            currentPitch = 0f;
        }

        inputActions = new InputSystem_Actions();
        FindXROriginRoot();

        if (GetComponent<StationWalkthrough.ControlsTutorialUI>() == null)
            gameObject.AddComponent<StationWalkthrough.ControlsTutorialUI>();
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        inputActions.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        inputActions.Disable();
    }

    private void Start()
    {
        if (movementJoystick == null)
        {
#if UNITY_2023_1_OR_NEWER
            Joystick[] joysticks = FindObjectsByType<Joystick>(FindObjectsSortMode.None);
#else
            Joystick[] joysticks = FindObjectsOfType<Joystick>();
#endif
            foreach (var joy in joysticks)
            {
                if (joy.gameObject.name.ToLower().Contains("fixed") || joy.gameObject.name.ToLower().Contains("move"))
                {
                    movementJoystick = joy;
                    break;
                }
            }
            if (movementJoystick == null && joysticks.Length > 0) movementJoystick = joysticks[0];
        }

        if (lookJoystick == null)
        {
#if UNITY_2023_1_OR_NEWER
            Joystick[] joysticks = FindObjectsByType<Joystick>(FindObjectsSortMode.None);
#else
            Joystick[] joysticks = FindObjectsOfType<Joystick>();
#endif
            foreach (var joy in joysticks)
            {
                string joyName = joy.gameObject.name.ToLower();
                if (lookJoystick == null && (joyName.Contains("look") || joyName.Contains("right")))
                    lookJoystick = joy;
            }

            // Fallbacks
            if (lookJoystick == null && joysticks.Length > 1) lookJoystick = joysticks[1];
        }

        if (lookJoystick != null && lookJoystick.AxisOptions == AxisOptions.Horizontal)
        {
            lookJoystick.AxisOptions = AxisOptions.Both;
        }

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
            initialCameraRotation = playerCamera.localRotation;
        }

        if (mobileUICanvas == null && movementJoystick != null)
        {
            Canvas parentCanvas = movementJoystick.GetComponentInParent<Canvas>();
            if (parentCanvas != null) mobileUICanvas = parentCanvas.gameObject;
        }

        ApplyPlatformSettings();
        CreateResetButton();
    }

    private void Update()
    {
        if (inputActions.Player.Jump.WasPressedThisFrame()) uiJumpTriggered = true;

        if (snapTurnTimer > 0f) snapTurnTimer -= Time.deltaTime;

        HandleRotation();
        HandleMovement();
        HandleCursorLock();
    }

    public bool CheckIsVRActive()
    {
        if (controlMode == ControlMode.ForceVR) return true;
        if (controlMode == ControlMode.ForceMobileTouch) return false;
        return XRSettings.isDeviceActive;
    }

    private void ApplyPlatformSettings()
    {
        isVRActive = CheckIsVRActive();
        if (mobileUICanvas != null) mobileUICanvas.SetActive(!isVRActive);

        if (playerCamera != null)
        {
            var drivers = playerCamera.GetComponents<MonoBehaviour>();
            foreach (var d in drivers) if (d != null && d.GetType().Name.Contains("TrackedPoseDriver")) d.enabled = isVRActive;

            Camera cam = playerCamera.GetComponent<Camera>();
            if (cam != null) cam.stereoTargetEye = isVRActive ? StereoTargetEyeMask.Both : StereoTargetEyeMask.None;
        }
    }

    private void FindXROriginRoot()
    {
        Transform current = transform;
        while (current != null)
        {
            foreach (var comp in current.GetComponents<MonoBehaviour>())
            {
                if (comp != null && comp.GetType().Name == "XROrigin")
                {
                    xrOriginRoot = current;
                    return;
                }
            }
            current = current.parent;
        }
        xrOriginRoot = transform;
    }

    private void HandleRotation()
    {
        float lookX = 0f;
        float lookY = 0f;

        // 1. Input System (Gamepad / VR right stick)
        Vector2 lookInput = inputActions.Player.Look.ReadValue<Vector2>();

        // Prevent pointer delta (Mouse/Touch) from being processed by the generic Input System here
        // so it doesn't rotate the camera when dragging the on-screen joystick or when unlocked.
        if (inputActions.Player.Look.activeControl != null && inputActions.Player.Look.activeControl.device is Pointer)
        {
            lookInput = Vector2.zero;
        }

        // VR Fallback Right Hand
        if (isVRActive && lookInput.sqrMagnitude <= inputDeadzone * inputDeadzone)
        {
            var rightHandDevices = new List<UnityEngine.XR.InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);
            foreach (var device in rightHandDevices)
            {
                if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis) && axis.sqrMagnitude > inputDeadzone * inputDeadzone)
                {
                    lookInput = axis;
                    break;
                }
            }
        }

        if (lookInput.sqrMagnitude > inputDeadzone * inputDeadzone)
        {
            if (isVRActive && useVRSnapTurn)
            {
                if (snapTurnTimer <= 0f && Mathf.Abs(lookInput.x) > 0.5f)
                {
                    lookX += Mathf.Sign(lookInput.x) * vrSnapTurnAngle;
                    snapTurnTimer = vrSnapTurnCooldown;
                }
            }
            else
            {
                lookX += lookInput.x * keyboardTurnSpeed * Time.deltaTime;
                if (!isVRActive) lookY += lookInput.y * keyboardTurnSpeed * Time.deltaTime;
            }
        }

        // 2. Keyboard Arrows (Look Around)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.rightArrowKey.isPressed) lookX += keyboardTurnSpeed * Time.deltaTime;
            if (Keyboard.current.leftArrowKey.isPressed) lookX -= keyboardTurnSpeed * Time.deltaTime;
            if (!isVRActive)
            {
                if (Keyboard.current.upArrowKey.isPressed) lookY += keyboardTurnSpeed * Time.deltaTime;
                if (Keyboard.current.downArrowKey.isPressed) lookY -= keyboardTurnSpeed * Time.deltaTime;
            }
        }

        // 3. Virtual Look Joystick (Right Joystick)
        if (!isVRActive && lookJoystick != null && lookJoystick.gameObject.activeInHierarchy)
        {
            if (lookJoystick.Direction.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                // Horizontal (X) and Vertical (Y) look rotation
                lookX += lookJoystick.Direction.x * joystickLookSensitivity * Time.deltaTime;
                lookY += lookJoystick.Direction.y * joystickLookSensitivity * Time.deltaTime;
            }
        }

        // 4. Touch Look (Free Screen Dragging)
        if (!isVRActive && EnhancedTouchSupport.enabled)
        {
            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    // Restrict touch rotation strictly to the right half of the screen
                    // This guarantees the left movement joystick won't accidentally spin the camera
                    if (touch.screenPosition.x > Screen.width * 0.5f && lookTouchFingerId == -1)
                    {
                        bool isOverUI = false;
                        if (UnityEngine.EventSystems.EventSystem.current != null)
                        {
                            // Check both ID types to ensure UI touches (like buttons) are ignored properly
                            isOverUI = UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.finger.index) ||
                                       UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.touchId);
                        }

                        if (!isOverUI)
                        {
                            lookTouchFingerId = touch.finger.index;
                            lastLookTouchPos = touch.screenPosition;
                        }
                    }
                }
                else if (touch.finger.index == lookTouchFingerId)
                {
                    if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                    {
                        Vector2 delta = touch.screenPosition - lastLookTouchPos;
                        lastLookTouchPos = touch.screenPosition;

                        // Apply both horizontal and vertical look rotation
                        lookX += delta.x * touchSensitivity;
                        lookY += delta.y * touchSensitivity;
                    }
                    else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        lookTouchFingerId = -1;
                    }
                }
            }
        }

        // 5. Mouse Look (Editor / Desktop)
        if (!isVRActive && Mouse.current != null)
        {
            bool canMouseLook = false;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                canMouseLook = true;
            }
            else if (Mouse.current.rightButton.isPressed)
            {
                canMouseLook = true; // Allow looking around while holding right click if unlocked
            }

            if (canMouseLook)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                lookX += delta.x * mouseSensitivity * 0.1f;
                lookY += delta.y * mouseSensitivity * 0.1f;
            }
        }

        yaw += lookX;

        // Apply vertical look (pitch) with user-defined rotation limits (e.g. ±60° to ±80°)
        if (invertY)
            pitch += lookY;
        else
            pitch -= lookY;

        float actualMin = Mathf.Min(minPitch, maxPitch);
        float actualMax = Mathf.Max(minPitch, maxPitch);
        pitch = Mathf.Clamp(pitch, actualMin, actualMax);

        if (isVRActive)
        {
            currentYaw = yaw; // Keep VR snap turning instant to prevent motion sickness
            currentPitch = pitch;
            Transform rotTarget = xrOriginRoot != null ? xrOriginRoot : transform;
            rotTarget.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        }
        else
        {
            // Apply smoothing for mobile/desktop
            if (rotationSmoothTime > 0f)
            {
                currentYaw = Mathf.SmoothDamp(currentYaw, yaw, ref yawVelocity, rotationSmoothTime);
                currentPitch = Mathf.SmoothDamp(currentPitch, pitch, ref pitchVelocity, rotationSmoothTime);
            }
            else
            {
                currentYaw = yaw;
                currentPitch = pitch;
            }
            currentPitch = Mathf.Clamp(currentPitch, actualMin, actualMax);

            if (playerCamera != null && playerCamera == transform)
            {
                // Script is attached directly to the Camera
                transform.localRotation = initialCameraRotation * Quaternion.Euler(currentPitch, currentYaw, 0f);
            }
            else
            {
                // Script is attached to a parent body - vertical pitch only rotates camera view, not the body
                transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
                if (playerCamera != null) playerCamera.localRotation = initialCameraRotation * Quaternion.Euler(currentPitch, 0f, 0f);
            }
        }
    }

    private void HandleMovement()
    {
        Vector2 input = Vector2.zero;
        bool isSprinting = false;
        bool jumpPressed = uiJumpTriggered;
        uiJumpTriggered = false;

        // 1. Keyboard WASD (Highest Priority)
        // W = Forward, S = Backward, A = Left, D = Right
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            if (Keyboard.current.leftShiftKey.isPressed) isSprinting = true;
            if (Keyboard.current.spaceKey.wasPressedThisFrame) jumpPressed = true;
        }

        // 2. Input System / Gamepad (Only if keyboard is not being used)
        if (input == Vector2.zero)
        {
            Vector2 moveInput = inputActions.Player.Move.ReadValue<Vector2>();
            if (moveInput.sqrMagnitude > inputDeadzone * inputDeadzone) input = moveInput;
            if (inputActions.Player.Sprint.IsPressed()) isSprinting = true;
        }

        // 3. VR Fallbacks (Left Hand Move, Both Hands Jump)
        if (isVRActive)
        {
            if (input == Vector2.zero)
            {
                var leftHandDevices = new List<UnityEngine.XR.InputDevice>();
                InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftHandDevices);
                foreach (var device in leftHandDevices)
                {
                    if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis) && axis.sqrMagnitude > inputDeadzone * inputDeadzone)
                    {
                        input = axis;
                        break;
                    }
                }
            }

            bool vrJump = false;
            var allHands = new List<UnityEngine.XR.InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, allHands);
            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, allHands);
            foreach (var device in allHands)
            {
                if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool p) && p) vrJump = true;
                if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool s) && s) vrJump = true;
            }
            if (vrJump && !prevVRJumpState) jumpPressed = true;
            prevVRJumpState = vrJump;
        }

        // 4. Mobile Joystick
        if (input == Vector2.zero && movementJoystick != null && movementJoystick.gameObject.activeInHierarchy)
        {
            if (movementJoystick.Direction.sqrMagnitude > inputDeadzone * inputDeadzone) input = movementJoystick.Direction;
        }

        // Clean up input (Deadzone & Snapping)
        if (input.sqrMagnitude > 0.001f)
        {
            if (Mathf.Abs(input.x) < axisSnapThreshold) input.x = 0f;
            if (Mathf.Abs(input.y) < axisSnapThreshold) input.y = 0f;

            // Allow analog joystick speed by clamping instead of always normalizing to 1
            input = Vector2.ClampMagnitude(input, 1f);
        }

        // Calculate Direction based on exact forward vectors
        Vector3 bodyForward;
        Vector3 bodyRight;

        if (isVRActive && playerCamera != null)
        {
            // In VR, push forward based on where the headset is facing
            bodyForward = playerCamera.forward;
            bodyForward.y = 0f;
            if (bodyForward.sqrMagnitude < 0.001f) bodyForward = Vector3.forward; else bodyForward.Normalize();

            bodyRight = playerCamera.right;
            bodyRight.y = 0f;
            if (bodyRight.sqrMagnitude < 0.001f) bodyRight = Vector3.right; else bodyRight.Normalize();
        }
        else
        {
            // Non-VR: push forward based on the body's rotation
            bodyForward = transform.forward;
            bodyForward.y = 0f;
            if (bodyForward.sqrMagnitude < 0.001f) bodyForward = Vector3.forward; else bodyForward.Normalize();

            bodyRight = transform.right;
            bodyRight.y = 0f;
            if (bodyRight.sqrMagnitude < 0.001f) bodyRight = Vector3.right; else bodyRight.Normalize();
        }

        Vector3 moveDir = (bodyForward * input.y + bodyRight * input.x);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        float speed = isSprinting ? sprintSpeed : moveSpeed;
        Vector3 targetVel = moveDir * speed;

        Transform moveTarget = (isVRActive && xrOriginRoot != null) ? xrOriginRoot : transform;

        // Physics-less Wall Collision (CapsuleCast)
        Vector3 finalMove = targetVel * Time.deltaTime;

        if (enableWallCollision && finalMove.sqrMagnitude > 0.00001f)
        {
            bool isCamera = (moveTarget.GetComponent<Camera>() != null);
            // Calculate capsule points (bottom is feet, top is head). 
            // We add 0.05f (skin width) to the bottom so it doesn't drag on the floor mesh colliders, preventing micro-jitters!
            Vector3 bottom = moveTarget.position + (isCamera ? Vector3.down * (playerHeight - playerRadius - 0.05f) : Vector3.up * (playerRadius + 0.05f));
            Vector3 top = moveTarget.position + (isCamera ? Vector3.down * playerRadius : Vector3.up * (playerHeight - playerRadius));

            // Only cast horizontal movement to prevent getting stuck on tiny floor bumps
            Vector3 horizontalMove = new Vector3(finalMove.x, 0f, finalMove.z);
            if (horizontalMove.sqrMagnitude > 0.00001f)
            {
                if (Physics.CapsuleCast(bottom, top, playerRadius, horizontalMove.normalized, out RaycastHit hit, horizontalMove.magnitude, wallCollisionLayer, QueryTriggerInteraction.Ignore))
                {
                    // Slide smoothly along the wall instead of stopping completely
                    horizontalMove = Vector3.ProjectOnPlane(horizontalMove, hit.normal);
                    finalMove.x = horizontalMove.x;
                    finalMove.z = horizontalMove.z;
                }
            }
        }

        // Jumping & Physics-less Gravity
        bool isOnGround = moveTarget.position.y <= spawnPosition.y + 0.01f;

        if (jumpPressed && isOnGround) verticalVelocity = Mathf.Sqrt(2f * jumpHeight * Mathf.Abs(gravity));

        if (!isOnGround || verticalVelocity > 0f) verticalVelocity += gravity * Time.deltaTime;
        else verticalVelocity = 0f;

        finalMove.y += verticalVelocity * Time.deltaTime;
        moveTarget.position += finalMove;

        // Floor collision
        if (moveTarget.position.y < spawnPosition.y)
        {
            moveTarget.position = new Vector3(moveTarget.position.x, spawnPosition.y, moveTarget.position.z);
            verticalVelocity = 0f;
        }
    }

    public void OnJumpPressed() { uiJumpTriggered = true; }

    public void SetJoystick(Joystick joystick) { movementJoystick = joystick; }

    public void ResetPosition()
    {
        yaw = spawnRotation.eulerAngles.y;
        currentYaw = yaw;
        pitch = 0f;
        currentPitch = 0f;
        yawVelocity = 0f;
        pitchVelocity = 0f;
        Transform moveTarget = (isVRActive && xrOriginRoot != null) ? xrOriginRoot : transform;
        moveTarget.position = spawnPosition;
        moveTarget.rotation = spawnRotation;
        verticalVelocity = 0f;
        if (playerCamera != null && !isVRActive)
        {
            if (playerCamera == transform) playerCamera.localRotation = initialCameraRotation * Quaternion.Euler(0f, currentYaw, 0f);
            else playerCamera.localRotation = initialCameraRotation;
        }
    }

    private void CreateResetButton()
    {
        if (isVRActive) return;
        Canvas canvas = null;
        if (mobileUICanvas != null) canvas = mobileUICanvas.GetComponent<Canvas>();
        if (canvas == null)
        {
#if UNITY_2023_1_OR_NEWER
            canvas = FindFirstObjectByType<Canvas>();
#else
            canvas = FindObjectOfType<Canvas>();
#endif
        }
        if (canvas == null) return;
        if (canvas.transform.Find("ResetPosButton") != null) return;

        GameObject btnObj = new GameObject("ResetPosButton");
        btnObj.transform.SetParent(canvas.transform, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0f, 1f);
        btnRect.anchorMax = new Vector2(0f, 1f);
        btnRect.pivot = new Vector2(0f, 1f);
        btnRect.anchoredPosition = new Vector2(20f, -20f);
        btnRect.sizeDelta = new Vector2(130f, 50f);

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0.85f, 0.25f, 0.2f, 0.9f);

        Button resetBtn = btnObj.AddComponent<Button>();
        resetBtn.onClick.AddListener(ResetPosition);

        GameObject textObj = new GameObject("ResetBtnText");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text btnText = textObj.AddComponent<Text>();
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        btnText.fontSize = 16;
        btnText.fontStyle = FontStyle.Bold;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;
        btnText.text = "⟲ RESET POS";
    }

    private void HandleCursorLock()
    {
        if (lockCursorOnDesktop && !isVRActive)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
