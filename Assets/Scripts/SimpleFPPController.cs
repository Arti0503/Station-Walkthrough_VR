using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.XR;
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
    public float touchSensitivity = 0.012f;
    public float joystickLookSensitivity = 60f;
    public float keyboardTurnSpeed = 90f;
    public bool lockCursorOnDesktop = true;
    public float rotationSmoothTime = 0.08f;

    [Header("Pitch / Vertical Look Limits")]
    [Tooltip("Minimum vertical look angle in degrees (looking up, e.g. -80)")]
    public float minPitch = -80f;
    [Tooltip("Maximum vertical look angle in degrees (looking down, e.g. 80)")]
    public float maxPitch = 80f;
    [Tooltip("Invert vertical look axis")]
    public bool invertY = false;

    [Header("VR Settings")]
    public Transform pitchPivot;
    public float vrTurnSpeed = 90f;
    public bool vrHeadOrientedMovement = true;
    public bool useVRSnapTurn = true;
    public float vrSnapTurnAngle = 45f;
    public float vrSnapTurnThreshold = 0.5f;
    public float vrSnapTurnRepeatDelay = 0.4f;
    public float vrSnapTurnCooldown = 0.2f;

    [Header("VR Optional Vertical Look (Right Stick Up/Down)")]
    [Tooltip("Allow Right Controller Joystick up/down to tilt camera view vertically in VR (Optional)")]
    public bool enableVRVerticalLook = false;
    [Tooltip("Legacy serialized field alias for vertical look in VR")]
    public bool enableVRPitchControl = false;
    public bool useVRSnapPitch = false;
    public float vrSnapPitchAngle = 15f;

    [Header("VR Controller Anchors")]
    public Transform leftControllerAnchor;
    public Transform rightControllerAnchor;
    public bool autoCreateControllerAnchors = false;
    public bool showVisualControllersInVR = false;
    public GameObject leftControllerPrefab;
    public GameObject rightControllerPrefab;

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
    private Quaternion initialPitchPivotRotation = Quaternion.identity;

    // State
    private Transform xrOriginRoot;
    private InputSystem_Actions inputActions;
    private bool prevVRJumpState = false;
    private bool uiJumpTriggered = false;
    private float snapTurnTimer = 0f;
    private float snapPitchTimer = 0f;
    private float vrPollTimer = 0f;
    private float vrPollDuration = 5f;

    // Dedicated Dynamic VR Input Actions (Direct hardware bindings for Quest controllers)
    private InputAction vrMoveAction;
    private InputAction vrTurnAction;
    private InputAction vrJumpAction;
    private InputAction vrSprintAction;
    private InputAction vrResetAction;

    // Touch State
    private int lookTouchFingerId = -1;
    private Vector2 lastLookTouchPos;

    private void Awake()
    {
        FindXROriginRoot();
        Transform target = (xrOriginRoot != null) ? xrOriginRoot : transform;
        spawnPosition = target.position;
        spawnRotation = target.rotation;
        yaw = target.eulerAngles.y;
        currentYaw = yaw;

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (playerCamera != null)
        {
            initialCameraRotation = playerCamera.localRotation;
            pitch = 0f;
            currentPitch = 0f;
        }

        // Sync vertical look aliases
        if (enableVRPitchControl) enableVRVerticalLook = true;
        else if (enableVRVerticalLook) enableVRPitchControl = true;

        inputActions = new InputSystem_Actions();
        SetupDynamicVRInputActions();

        FindControllerAnchors();
        SetupPitchPivot();

        if (GetComponent<StationWalkthrough.ControlsTutorialUI>() == null)
            gameObject.AddComponent<StationWalkthrough.ControlsTutorialUI>();
    }

    private void SetupDynamicVRInputActions()
    {
        // 1. VR Movement Action (Left Thumbstick)
        vrMoveAction = new InputAction("VR_Move", InputActionType.Value, "<XRController>{LeftHand}/thumbstick");
        vrMoveAction.AddBinding("<XRController>{LeftHand}/primary2DAxis");
        vrMoveAction.AddBinding("<OculusTouchController>{LeftHand}/thumbstick");
        vrMoveAction.AddBinding("<OculusTouchController>{LeftHand}/primary2DAxis");

        // 2. VR Turning Action (Right Thumbstick)
        vrTurnAction = new InputAction("VR_Turn", InputActionType.Value, "<XRController>{RightHand}/thumbstick");
        vrTurnAction.AddBinding("<XRController>{RightHand}/primary2DAxis");
        vrTurnAction.AddBinding("<OculusTouchController>{RightHand}/thumbstick");
        vrTurnAction.AddBinding("<OculusTouchController>{RightHand}/primary2DAxis");

        // 3. VR Jump Action ('A' Button Right Hand or 'X' Button Left Hand)
        vrJumpAction = new InputAction("VR_Jump", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        vrJumpAction.AddBinding("<XRController>{LeftHand}/primaryButton");
        vrJumpAction.AddBinding("<OculusTouchController>{RightHand}/primaryButton");
        vrJumpAction.AddBinding("<OculusTouchController>{LeftHand}/primaryButton");

        // 4. VR Sprint Action (Left Thumbstick Click or Grip)
        vrSprintAction = new InputAction("VR_Sprint", InputActionType.Button, "<XRController>{LeftHand}/thumbstickClicked");
        vrSprintAction.AddBinding("<XRController>{LeftHand}/gripPressed");
        vrSprintAction.AddBinding("<XRController>{LeftHand}/gripButton");
        vrSprintAction.AddBinding("<OculusTouchController>{LeftHand}/thumbstickClicked");
        vrSprintAction.AddBinding("<OculusTouchController>{LeftHand}/gripPressed");

        // 5. VR Reset Position Action (Menu button or dual stick click)
        vrResetAction = new InputAction("VR_Reset", InputActionType.Button, "<XRController>/menu");
        vrResetAction.AddBinding("<XRController>/menuButton");
        vrResetAction.AddBinding("<OculusTouchController>/menu");
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        inputActions?.Enable();
        vrMoveAction?.Enable();
        vrTurnAction?.Enable();
        vrJumpAction?.Enable();
        vrSprintAction?.Enable();
        vrResetAction?.Enable();

        InputDevices.deviceConnected += OnXRDeviceConnected;
        InputDevices.deviceDisconnected += OnXRDeviceDisconnected;
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        inputActions?.Disable();
        vrMoveAction?.Disable();
        vrTurnAction?.Disable();
        vrJumpAction?.Disable();
        vrSprintAction?.Disable();
        vrResetAction?.Disable();

        InputDevices.deviceConnected -= OnXRDeviceConnected;
        InputDevices.deviceDisconnected -= OnXRDeviceDisconnected;
    }

    private void OnDestroy()
    {
        vrMoveAction?.Dispose();
        vrTurnAction?.Dispose();
        vrJumpAction?.Dispose();
        vrSprintAction?.Dispose();
        vrResetAction?.Dispose();
    }

    private void OnXRDeviceConnected(UnityEngine.XR.InputDevice device)
    {
        ApplyPlatformSettings();
    }

    private void OnXRDeviceDisconnected(UnityEngine.XR.InputDevice device)
    {
    }

    private void Start()
    {
        if (movementJoystick == null)
        {
#if UNITY_2023_1_OR_NEWER
            movementJoystick = FindFirstObjectByType<Joystick>();
#else
            movementJoystick = FindObjectOfType<Joystick>();
#endif
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
        // Poll for asynchronous OpenXR initialization
        if (!isVRActive && vrPollDuration > 0f)
        {
            vrPollDuration -= Time.deltaTime;
            vrPollTimer -= Time.deltaTime;
            if (vrPollTimer <= 0f)
            {
                vrPollTimer = 0.5f;
                if (CheckIsVRActive())
                {
                    ApplyPlatformSettings();
                }
            }
        }

        if (inputActions.Player.Jump.WasPressedThisFrame()) uiJumpTriggered = true;

        if (snapTurnTimer > 0f) snapTurnTimer -= Time.deltaTime;
        if (snapPitchTimer > 0f) snapPitchTimer -= Time.deltaTime;

        HandleRotation();
        HandleMovement();
        HandleCursorLock();
    }

    public bool CheckIsVRActive()
    {
        if (controlMode == ControlMode.ForceVR) return true;
        if (controlMode == ControlMode.ForceMobileTouch) return false;

        if (XRSettings.isDeviceActive) return true;

        try
        {
            if (UnityEngine.XR.Management.XRGeneralSettings.Instance != null &&
                UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager != null &&
                UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader != null)
            {
                return true;
            }
        }
        catch { }

#if UNITY_ANDROID && !UNITY_EDITOR
        var model = SystemInfo.deviceModel;
        if (!string.IsNullOrEmpty(model) && (model.Contains("Quest") || model.Contains("Oculus") || model.Contains("Eureka") || model.Contains("Pacific")))
            return true;
#endif

        return false;
    }

    private void ApplyPlatformSettings()
    {
        isVRActive = CheckIsVRActive();
        if (mobileUICanvas != null) mobileUICanvas.SetActive(!isVRActive);

        if (playerCamera != null)
        {
            var drivers = playerCamera.GetComponents<MonoBehaviour>();
            foreach (var d in drivers) 
            {
                if (d != null && d.GetType().Name.Contains("TrackedPoseDriver")) 
                {
                    d.enabled = isVRActive;
                }
            }

            Camera cam = playerCamera.GetComponent<Camera>();
            if (cam != null)
            {
                cam.stereoTargetEye = isVRActive ? StereoTargetEyeMask.Both : StereoTargetEyeMask.None;

                if (cam.nearClipPlane < 0.15f) cam.nearClipPlane = 0.15f;
                if (cam.farClipPlane > 500f) cam.farClipPlane = 400f;
            }
        }

        if (isVRActive)
        {
            FindControllerAnchors();
            SetupPitchPivot();
            SetupVRControllers();
        }
    }

    private void SetupVRControllers()
    {
        if (playerCamera == null) return;
        Transform cameraOffset = playerCamera.parent != null ? playerCamera.parent : playerCamera;

        if (leftControllerAnchor == null && autoCreateControllerAnchors)
        {
            GameObject leftObj = new GameObject("Left Controller");
            leftObj.transform.SetParent(cameraOffset, false);
            leftObj.transform.localPosition = new Vector3(-0.2f, -0.1f, 0.3f);
            leftControllerAnchor = leftObj.transform;
        }

        if (rightControllerAnchor == null && autoCreateControllerAnchors)
        {
            GameObject rightObj = new GameObject("Right Controller");
            rightObj.transform.SetParent(cameraOffset, false);
            rightObj.transform.localPosition = new Vector3(0.2f, -0.1f, 0.3f);
            rightControllerAnchor = rightObj.transform;
        }

        if (leftControllerAnchor != null)
        {
            EnsureTrackedPoseDriver(leftControllerAnchor.gameObject, true);
            if (!showVisualControllersInVR) RemoveAllControllerVisuals(leftControllerAnchor);
        }

        if (rightControllerAnchor != null)
        {
            EnsureTrackedPoseDriver(rightControllerAnchor.gameObject, false);
            if (!showVisualControllersInVR) RemoveAllControllerVisuals(rightControllerAnchor);
        }
    }

    public static void RemoveAllControllerVisuals(Transform anchor)
    {
        if (anchor == null) return;
        List<GameObject> toDestroy = new List<GameObject>();
        for (int i = 0; i < anchor.childCount; i++)
        {
            toDestroy.Add(anchor.GetChild(i).gameObject);
        }
        foreach (var obj in toDestroy)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(obj);
                else Destroy(obj);
#else
                Destroy(obj);
#endif
            }
        }

        var mr = anchor.GetComponent<MeshRenderer>();
        if (mr != null) { if (Application.isPlaying) Destroy(mr); else DestroyImmediate(mr); }
        var mf = anchor.GetComponent<MeshFilter>();
        if (mf != null) { if (Application.isPlaying) Destroy(mf); else DestroyImmediate(mf); }
        var col = anchor.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
    }

    private void EnsureTrackedPoseDriver(GameObject obj, bool isLeft)
    {
        var driver = obj.GetComponent<TrackedPoseDriver>();
        if (driver == null)
        {
            driver = obj.AddComponent<TrackedPoseDriver>();
        }
        driver.enabled = true;
        driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;

        string handTag = isLeft ? "{LeftHand}" : "{RightHand}";
        string posPath = $"<XRController>{handTag}/devicePosition";
        string rotPath = $"<XRController>{handTag}/deviceRotation";

        driver.positionInput = new InputActionProperty(new InputAction("Position", InputActionType.Value, posPath, expectedControlType: "Vector3"));
        driver.rotationInput = new InputActionProperty(new InputAction("Rotation", InputActionType.Value, rotPath, expectedControlType: "Quaternion"));
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

    private void FindControllerAnchors()
    {
        if (leftControllerAnchor == null || rightControllerAnchor == null)
        {
            Transform searchRoot = xrOriginRoot != null ? xrOriginRoot : transform;
            var allTransforms = searchRoot.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                string n = t.name.ToLower();
                if (leftControllerAnchor == null && n.Contains("left") && n.Contains("controller"))
                    leftControllerAnchor = t;
                if (rightControllerAnchor == null && n.Contains("right") && n.Contains("controller"))
                    rightControllerAnchor = t;
            }
        }
    }

    private void SetupPitchPivot()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (playerCamera == null) return;

        if (pitchPivot == null)
        {
            if (playerCamera.parent != null && playerCamera.parent.name.Contains("Pivot"))
            {
                pitchPivot = playerCamera.parent;
            }
            else
            {
                pitchPivot = playerCamera;
            }
        }

        if (pitchPivot != null)
        {
            initialPitchPivotRotation = pitchPivot.localRotation;
        }
    }

    private static bool IsLeftHandController(UnityEngine.InputSystem.InputDevice device)
    {
        if (device == null) return false;
        for (int i = 0; i < device.usages.Count; i++)
        {
            if (device.usages[i] == UnityEngine.InputSystem.CommonUsages.LeftHand) return true;
        }
        string devName = device.name != null ? device.name.ToLower() : "";
        return devName.Contains("left");
    }

    private static bool IsRightHandController(UnityEngine.InputSystem.InputDevice device)
    {
        if (device == null) return false;
        for (int i = 0; i < device.usages.Count; i++)
        {
            if (device.usages[i] == UnityEngine.InputSystem.CommonUsages.RightHand) return true;
        }
        string devName = device.name != null ? device.name.ToLower() : "";
        return devName.Contains("right");
    }

    /// <summary>
    /// Reads the Left VR Controller Joystick for Movement:
    /// Returns 2D Vector: X = Strafe Left/Right (-1 to 1), Y = Forward/Backward (-1 to 1)
    /// </summary>
    public Vector2 GetVRLeftJoystick()
    {
        // 1. Direct hardware InputAction (Highest priority, works on all Meta Quest headsets)
        if (vrMoveAction != null)
        {
            Vector2 actionVal = vrMoveAction.ReadValue<Vector2>();
            if (actionVal.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                return actionVal;
            }
        }

        // 2. Direct OpenXR InputDevices query (XRNode.LeftHand primary2DAxis)
        var leftDevices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftDevices);
        if (leftDevices.Count == 0)
        {
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, leftDevices);
        }
        foreach (var dev in leftDevices)
        {
            if (dev.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 val) && val.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                return val;
            }
            if (dev.TryGetFeatureValue(CommonUsages.secondary2DAxis, out Vector2 secVal) && secVal.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                return secVal;
            }
        }

        // 3. Direct Input System XR Controller devices query (Thumbstick / Primary2DAxis)
        foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
        {
            if (device is UnityEngine.InputSystem.XR.XRController ctrl && IsLeftHandController(device))
            {
                var stick = ctrl.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("thumbstick") 
                         ?? ctrl.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("primary2DAxis")
                         ?? ctrl.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("joystick");
                if (stick != null)
                {
                    Vector2 val = stick.ReadValue();
                    if (val.sqrMagnitude > inputDeadzone * inputDeadzone)
                    {
                        return val;
                    }
                }
            }
        }

        // 4. Input System Action query (Player.Move)
        if (inputActions != null)
        {
            Vector2 actionVal = inputActions.Player.Move.ReadValue<Vector2>();
            if (actionVal.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                return actionVal;
            }
        }

        // 5. Gamepad fallback (XR Simulator / Link gamepad emulation)
        if (UnityEngine.InputSystem.Gamepad.current != null)
        {
            Vector2 gpVal = UnityEngine.InputSystem.Gamepad.current.leftStick.ReadValue();
            if (gpVal.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                return gpVal;
            }
        }

        return Vector2.zero;
    }

    /// <summary>
    /// Reads the Right VR Controller Joystick for Rotation/Turning:
    /// Returns 2D Vector: X = Turn Left/Right (-1 to 1), Y = Look Up/Down (-1 to 1)
    /// </summary>
    public Vector2 GetVRRightJoystick()
    {
        // 1. Direct hardware InputAction (Highest priority, works on all Meta Quest headsets)
        if (vrTurnAction != null)
        {
            Vector2 actionVal = vrTurnAction.ReadValue<Vector2>();
            if (actionVal.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                return actionVal;
            }
        }

        // 2. Direct OpenXR InputDevices query (XRNode.RightHand primary2DAxis)
        var rightDevices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightDevices);
        if (rightDevices.Count == 0)
        {
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, rightDevices);
        }
        foreach (var dev in rightDevices)
        {
            if (dev.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 val) && val.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                return val;
            }
            if (dev.TryGetFeatureValue(CommonUsages.secondary2DAxis, out Vector2 secVal) && secVal.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                return secVal;
            }
        }

        // 3. Direct Input System XR Controller devices query (Thumbstick / Primary2DAxis)
        foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
        {
            if (device is UnityEngine.InputSystem.XR.XRController ctrl && IsRightHandController(device))
            {
                var stick = ctrl.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("thumbstick") 
                         ?? ctrl.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("primary2DAxis")
                         ?? ctrl.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("joystick");
                if (stick != null)
                {
                    Vector2 val = stick.ReadValue();
                    if (val.sqrMagnitude > inputDeadzone * inputDeadzone)
                    {
                        return val;
                    }
                }
            }
        }

        // 4. Input System Action query (Player.Look)
        if (inputActions != null)
        {
            if (inputActions.Player.Look.activeControl == null || !(inputActions.Player.Look.activeControl.device is Pointer))
            {
                Vector2 actionVal = inputActions.Player.Look.ReadValue<Vector2>();
                if (actionVal.sqrMagnitude > inputDeadzone * inputDeadzone)
                {
                    return actionVal;
                }
            }
        }

        // 5. Gamepad fallback (XR Simulator / Link gamepad emulation)
        if (UnityEngine.InputSystem.Gamepad.current != null)
        {
            Vector2 gpVal = UnityEngine.InputSystem.Gamepad.current.rightStick.ReadValue();
            if (gpVal.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                return gpVal;
            }
        }

        return Vector2.zero;
    }

    private void HandleRotation()
    {
        float lookX = 0f;
        float lookY = 0f;

        // Read VR Right Joystick (always checked so controls work instantly)
        Vector2 vrLook = GetVRRightJoystick();
        Vector2 lookInput = vrLook;

        if (lookInput.sqrMagnitude <= inputDeadzone * inputDeadzone)
        {
            // Non-VR Gamepad Look
            Vector2 rawLook = inputActions.Player.Look.ReadValue<Vector2>();
            if (inputActions.Player.Look.activeControl != null && inputActions.Player.Look.activeControl.device is Pointer)
            {
                rawLook = Vector2.zero;
            }
            if (rawLook.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                lookInput = rawLook;
            }
        }

        // Process Right Joystick (VR Right Thumbstick / Gamepad Right Stick)
        if (lookInput.sqrMagnitude > inputDeadzone * inputDeadzone)
        {
            if (isVRActive || vrLook.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                float repeatDelay = vrSnapTurnRepeatDelay > 0f ? vrSnapTurnRepeatDelay : vrSnapTurnCooldown;

                // --- VR Horizontal Look (Yaw): Turn Left / Right ---
                if (useVRSnapTurn)
                {
                    if (snapTurnTimer <= 0f && Mathf.Abs(lookInput.x) >= vrSnapTurnThreshold)
                    {
                        lookX += Mathf.Sign(lookInput.x) * vrSnapTurnAngle;
                        snapTurnTimer = repeatDelay;
                    }
                    else if (Mathf.Abs(lookInput.x) < vrSnapTurnThreshold * 0.5f)
                    {
                        snapTurnTimer = 0f;
                    }
                }
                else
                {
                    // Smooth horizontal turn
                    lookX += lookInput.x * vrTurnSpeed * Time.deltaTime;
                }

                // --- VR Vertical Look (Pitch): Optional Camera Up / Down ---
                if (enableVRVerticalLook || enableVRPitchControl)
                {
                    if (useVRSnapPitch)
                    {
                        if (snapPitchTimer <= 0f && Mathf.Abs(lookInput.y) >= vrSnapTurnThreshold)
                        {
                            lookY += Mathf.Sign(lookInput.y) * vrSnapPitchAngle;
                            snapPitchTimer = repeatDelay;
                        }
                        else if (Mathf.Abs(lookInput.y) < vrSnapTurnThreshold * 0.5f)
                        {
                            snapPitchTimer = 0f;
                        }
                    }
                    else
                    {
                        lookY += lookInput.y * vrTurnSpeed * Time.deltaTime;
                    }
                }
            }
            else
            {
                // Non-VR Gamepad
                lookX += lookInput.x * keyboardTurnSpeed * Time.deltaTime;
                lookY += lookInput.y * keyboardTurnSpeed * Time.deltaTime;
            }
        }

        // Keyboard Arrows
        if (Keyboard.current != null)
        {
            if (Keyboard.current.rightArrowKey.isPressed) lookX += keyboardTurnSpeed * Time.deltaTime;
            if (Keyboard.current.leftArrowKey.isPressed) lookX -= keyboardTurnSpeed * Time.deltaTime;
            if (!isVRActive || enableVRVerticalLook || enableVRPitchControl)
            {
                if (Keyboard.current.upArrowKey.isPressed) lookY += keyboardTurnSpeed * Time.deltaTime;
                if (Keyboard.current.downArrowKey.isPressed) lookY -= keyboardTurnSpeed * Time.deltaTime;
            }
        }

        // Virtual Look Joystick (Right Joystick on Mobile)
        if (!isVRActive && lookJoystick != null && lookJoystick.gameObject.activeInHierarchy)
        {
            if (lookJoystick.Direction.sqrMagnitude > inputDeadzone * inputDeadzone)
            {
                lookX += lookJoystick.Direction.x * joystickLookSensitivity * Time.deltaTime;
                lookY += lookJoystick.Direction.y * joystickLookSensitivity * Time.deltaTime;
            }
        }

        // Touch Look (Mobile drag)
        if (!isVRActive && EnhancedTouchSupport.enabled)
        {
            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    if (touch.screenPosition.x > Screen.width * 0.5f && lookTouchFingerId == -1)
                    {
                        bool isOverUI = false;
                        if (UnityEngine.EventSystems.EventSystem.current != null)
                        {
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

        // Mouse Look (Desktop)
        if (!isVRActive && Mouse.current != null)
        {
            bool canMouseLook = (Cursor.lockState == CursorLockMode.Locked) || Mouse.current.rightButton.isPressed;
            if (canMouseLook)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                lookX += delta.x * mouseSensitivity * 0.1f;
                lookY += delta.y * mouseSensitivity * 0.1f;
            }
        }

        yaw += lookX;

        if (invertY) pitch += lookY;
        else pitch -= lookY;

        float actualMin = Mathf.Min(minPitch, maxPitch);
        float actualMax = Mathf.Max(minPitch, maxPitch);
        pitch = Mathf.Clamp(pitch, actualMin, actualMax);

        if (isVRActive || vrLook.sqrMagnitude > inputDeadzone * inputDeadzone)
        {
            Transform rotTarget = xrOriginRoot != null ? xrOriginRoot : transform;
            if (Mathf.Abs(lookX) > 0.0001f)
            {
                Vector3 pivot = playerCamera != null ? playerCamera.position : rotTarget.position;
                rotTarget.RotateAround(pivot, Vector3.up, lookX);
                yaw = rotTarget.eulerAngles.y;
            }
            currentYaw = yaw;
            currentPitch = (enableVRVerticalLook || enableVRPitchControl) ? pitch : 0f;

            if (pitchPivot != null)
            {
                pitchPivot.localRotation = initialPitchPivotRotation * Quaternion.Euler(currentPitch, 0f, 0f);
            }
        }
        else
        {
            if (pitchPivot != null && pitchPivot.localRotation != initialPitchPivotRotation)
            {
                pitchPivot.localRotation = initialPitchPivotRotation;
            }

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
                transform.localRotation = initialCameraRotation * Quaternion.Euler(currentPitch, currentYaw, 0f);
            }
            else
            {
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

        // 1. Keyboard WASD
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            if (Keyboard.current.leftShiftKey.isPressed) isSprinting = true;
            if (Keyboard.current.spaceKey.wasPressedThisFrame) jumpPressed = true;
        }

        // 2. VR Controller Input (Always polled so Quest thumbstick works immediately)
        Vector2 vrMove = GetVRLeftJoystick();
        if (vrMove.sqrMagnitude > inputDeadzone * inputDeadzone)
        {
            input = vrMove;
        }

        // VR Sprint (Left Stick Click or Left Grip)
        if (vrSprintAction != null && vrSprintAction.IsPressed()) isSprinting = true;

        var leftDevices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftDevices);
        if (leftDevices.Count == 0) InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, leftDevices);
        foreach (var device in leftDevices)
        {
            if (device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool stickClick) && stickClick) isSprinting = true;
            if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripBtn) && gripBtn) isSprinting = true;
            if (device.TryGetFeatureValue(CommonUsages.grip, out float gripVal) && gripVal > 0.5f) isSprinting = true;
        }

        // VR Jump (A Button or X Button)
        bool vrJump = (vrJumpAction != null && vrJumpAction.IsPressed());
        var allHands = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, allHands);
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, allHands);
        if (allHands.Count == 0) InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, allHands);
        foreach (var device in allHands)
        {
            if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool p) && p) vrJump = true;
            if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool s) && s) vrJump = true;
        }
        if (vrJump && !prevVRJumpState) jumpPressed = true;
        prevVRJumpState = vrJump;

        // VR Reset Position (Menu button or dual stick click)
        bool menuPress = (vrResetAction != null && vrResetAction.IsPressed());
        bool leftStickClick = false;
        bool rightStickClick = false;
        foreach (var d in leftDevices)
        {
            if (d.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool c) && c) leftStickClick = true;
            if (d.TryGetFeatureValue(CommonUsages.menuButton, out bool m) && m) menuPress = true;
        }
        var rightDevices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightDevices);
        if (rightDevices.Count == 0) InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, rightDevices);
        foreach (var d in rightDevices)
        {
            if (d.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool c) && c) rightStickClick = true;
        }
        if ((leftStickClick && rightStickClick) || menuPress)
        {
            ResetPosition();
        }

        // 3. Input System Gamepad
        if (input == Vector2.zero)
        {
            Vector2 moveInput = inputActions.Player.Move.ReadValue<Vector2>();
            if (moveInput.sqrMagnitude > inputDeadzone * inputDeadzone) input = moveInput;
            if (inputActions.Player.Sprint.IsPressed()) isSprinting = true;
        }

        // 4. Mobile Joystick
        if (input == Vector2.zero && movementJoystick != null && movementJoystick.gameObject.activeInHierarchy)
        {
            if (movementJoystick.Direction.sqrMagnitude > inputDeadzone * inputDeadzone) input = movementJoystick.Direction;
        }

        // Clean up input
        if (input.sqrMagnitude > 0.001f)
        {
            if (Mathf.Abs(input.x) < axisSnapThreshold) input.x = 0f;
            if (Mathf.Abs(input.y) < axisSnapThreshold) input.y = 0f;
            input = Vector2.ClampMagnitude(input, 1f);
        }

        // Calculate Direction based on forward vectors
        Vector3 bodyForward;
        Vector3 bodyRight;

        if (isVRActive || vrMove.sqrMagnitude > inputDeadzone * inputDeadzone)
        {
            Transform orientationRef = (vrHeadOrientedMovement || leftControllerAnchor == null) ? playerCamera : leftControllerAnchor;
            if (orientationRef != null)
            {
                bodyForward = orientationRef.forward;
                bodyForward.y = 0f;
                if (bodyForward.sqrMagnitude < 0.001f) bodyForward = Vector3.forward; else bodyForward.Normalize();

                bodyRight = orientationRef.right;
                bodyRight.y = 0f;
                if (bodyRight.sqrMagnitude < 0.001f) bodyRight = Vector3.right; else bodyRight.Normalize();
            }
            else
            {
                bodyForward = transform.forward;
                bodyForward.y = 0f;
                if (bodyForward.sqrMagnitude < 0.001f) bodyForward = Vector3.forward; else bodyForward.Normalize();

                bodyRight = transform.right;
                bodyRight.y = 0f;
                if (bodyRight.sqrMagnitude < 0.001f) bodyRight = Vector3.right; else bodyRight.Normalize();
            }
        }
        else
        {
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

        Transform moveTarget = (xrOriginRoot != null) ? xrOriginRoot : transform;

        // Physics-less Wall Collision (CapsuleCastAll with self-collider filter)
        Vector3 finalMove = targetVel * Time.deltaTime;

        if (enableWallCollision && finalMove.sqrMagnitude > 0.00001f)
        {
            bool isCamera = (moveTarget.GetComponent<Camera>() != null);
            Vector3 bottom = moveTarget.position + (isCamera ? Vector3.down * (playerHeight - playerRadius - 0.05f) : Vector3.up * (playerRadius + 0.05f));
            Vector3 top = moveTarget.position + (isCamera ? Vector3.down * playerRadius : Vector3.up * (playerHeight - playerRadius));

            Vector3 horizontalMove = new Vector3(finalMove.x, 0f, finalMove.z);
            if (horizontalMove.sqrMagnitude > 0.00001f)
            {
                RaycastHit[] hits = Physics.CapsuleCastAll(bottom, top, playerRadius, horizontalMove.normalized, horizontalMove.magnitude, wallCollisionLayer, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    if (hit.collider != null && !hit.collider.transform.IsChildOf(moveTarget))
                    {
                        horizontalMove = Vector3.ProjectOnPlane(horizontalMove, hit.normal);
                        finalMove.x = horizontalMove.x;
                        finalMove.z = horizontalMove.z;
                        break;
                    }
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
        Transform moveTarget = (xrOriginRoot != null) ? xrOriginRoot : transform;
        moveTarget.position = spawnPosition;
        moveTarget.rotation = spawnRotation;
        verticalVelocity = 0f;
        if (playerCamera != null && !isVRActive)
        {
            if (playerCamera == transform) playerCamera.localRotation = initialCameraRotation * Quaternion.Euler(0f, currentYaw, 0f);
            else playerCamera.localRotation = initialCameraRotation;
        }
        if (pitchPivot != null)
        {
            pitchPivot.localRotation = initialPitchPivotRotation;
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
