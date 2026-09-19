using UnityEngine;
using UnityEngine.UI;

namespace StationWalkthrough
{
    /// <summary>
    /// Displays an in-game tutorial and caution overlay for controls on Android Touch and Meta Quest VR.
    /// </summary>
    public class ControlsTutorialUI : MonoBehaviour
    {
        [Header("Tutorial Display Settings")]
        [Tooltip("Show the tutorial automatically when the game starts")]
        public bool showOnStart = true;

        [Tooltip("Auto-hide tutorial after this many seconds (0 to keep open until dismissed)")]
        public float autoHideDelay = 8f;

        [Header("Optional Custom UI References (Leave empty to auto-generate UI)")]
        public GameObject tutorialPanel;
        public Text tutorialText;
        public Button closeButton;
        public Button helpButton;

        [Header("Panel & Font Customization (Adjust Size Here)")]
        [Tooltip("Width and Height of the tutorial panel in pixels (Default: 850 x 480)")]
        public Vector2 panelSize = new Vector2(850f, 480f);

        [Tooltip("Font size for the header title")]
        public int titleFontSize = 24;

        [Tooltip("Font size for the body instructions")]
        public int bodyFontSize = 18;

        [Tooltip("Size of the 'Got It' Dismiss Button")]
        public Vector2 buttonSize = new Vector2(240f, 50f);

        private RectTransform panelRectTransform;
        private SimpleFPPController controller;
        private bool isVR;

        private void Start()
        {
            controller = FindFirstObjectByType<SimpleFPPController>();
            if (controller == null)
            {
#if !UNITY_2023_1_OR_NEWER
                controller = FindObjectOfType<SimpleFPPController>();
#endif
            }

            isVR = controller != null && controller.CheckIsVRActive();

            if (tutorialPanel == null)
            {
                CreateBuiltInTutorialUI();
            }
            else
            {
                panelRectTransform = tutorialPanel.GetComponent<RectTransform>();
                UpdateTutorialText();
                if (closeButton != null) closeButton.onClick.AddListener(HideTutorial);
                if (helpButton != null) helpButton.onClick.AddListener(ShowTutorial);
            }

            ApplySizeSettings();

            if (showOnStart)
            {
                ShowTutorial();
                if (autoHideDelay > 0f)
                {
                    Invoke(nameof(HideTutorial), autoHideDelay);
                }
            }
            else
            {
                HideTutorial();
            }
        }

        public void ApplySizeSettings()
        {
            if (panelRectTransform != null)
            {
                panelRectTransform.sizeDelta = panelSize;
            }
            if (tutorialText != null)
            {
                tutorialText.fontSize = bodyFontSize;
            }
        }

        private void OnValidate()
        {
            ApplySizeSettings();
            UpdateTutorialText();
        }

        public void ShowTutorial()
        {
            CancelInvoke(nameof(HideTutorial));
            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(true);
                UpdateTutorialText();
                ApplySizeSettings();
            }
        }

        public void HideTutorial()
        {
            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(false);
            }
        }

        public void ToggleTutorial()
        {
            if (tutorialPanel != null)
            {
                if (tutorialPanel.activeSelf) HideTutorial();
                else ShowTutorial();
            }
        }

        private void UpdateTutorialText()
        {
            if (tutorialText == null) return;

            isVR = controller != null && controller.CheckIsVRActive();

            if (isVR)
            {
                tutorialText.text =
                    $"<b><size={titleFontSize}>🥽 VR CONTROLS & CAUTION</size></b>\n\n" +
                    "• <b>Move:</b> Left Thumbstick (Camera-relative walk)\n" +
                    "• <b>Rotate / Turn:</b> Right Thumbstick (or turn head)\n" +
                    "• <b>Jump:</b> 'A' Button (Right hand) or 'X' Button (Left hand)\n" +
                    "• <b>Sprint:</b> Hold 'Y' Button or Left Grip Trigger\n\n" +
                    "<b><color=#FFA500>⚠️ CAUTION & SAFETY:</color></b>\n" +
                    "1. Ensure you have a clear guardian boundary before moving.\n" +
                    "2. If experiencing motion sickness, pause or rest immediately.";
            }
            else
            {
                tutorialText.text =
                    $"<b><size={titleFontSize}>📱 MOBILE TOUCH CONTROLS & CAUTION</size></b>\n\n" +
                    "• <b>Move:</b> Drag Virtual Joystick on the bottom-left\n" +
                    "• <b>Rotate / Look:</b> Swipe & drag across the right side of the screen\n" +
                    "• <b>Jump:</b> Tap the Jump Button on the bottom-right\n\n" +
                    "<b><color=#FFA500>⚠️ CAUTION:</color></b>\n" +
                    "1. Controls automatically switch to VR mode when a headset is connected.\n" +
                    "2. Keep a steady swipe motion for smooth camera navigation.";
            }
        }

        /// <summary>
        /// Generates a clean, modern on-screen tutorial overlay dynamically.
        /// </summary>
        private void CreateBuiltInTutorialUI()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
#if UNITY_2023_1_OR_NEWER
                canvas = FindFirstObjectByType<Canvas>();
#else
                canvas = FindObjectOfType<Canvas>();
#endif
            }

            if (canvas == null) return;

            // 1. Create Main Tutorial Panel
            GameObject panelObj = new GameObject("TutorialCautionPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            panelRectTransform = panelObj.AddComponent<RectTransform>();
            panelRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            panelRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            panelRectTransform.pivot = new Vector2(0.5f, 0.5f);
            panelRectTransform.sizeDelta = panelSize;

            Image panelBg = panelObj.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.1f, 0.14f, 0.92f); // Dark translucent theme
            tutorialPanel = panelObj;

            // 2. Create Text Content
            GameObject textObj = new GameObject("TutorialText");
            textObj.transform.SetParent(panelObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.06f, 0.2f);
            textRect.anchorMax = new Vector2(0.94f, 0.95f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            tutorialText = textObj.AddComponent<Text>();
            tutorialText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (tutorialText.font == null) tutorialText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            tutorialText.fontSize = bodyFontSize;
            tutorialText.alignment = TextAnchor.UpperLeft;
            tutorialText.color = Color.white;
            tutorialText.supportRichText = true;

            // 3. Create Dismiss / Close Button
            GameObject btnObj = new GameObject("CloseTutorialButton");
            btnObj.transform.SetParent(panelObj.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.05f);
            btnRect.anchorMax = new Vector2(0.5f, 0.05f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.sizeDelta = buttonSize;

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.18f, 0.55f, 0.92f, 1f); // Vibrant accent blue

            closeButton = btnObj.AddComponent<Button>();
            closeButton.onClick.AddListener(HideTutorial);

            GameObject btnTextObj = new GameObject("BtnText");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            Text btnText = btnTextObj.AddComponent<Text>();
            btnText.font = tutorialText.font;
            btnText.fontSize = 16;
            btnText.fontStyle = FontStyle.Bold;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.text = "GOT IT (DISMISS)";

            // 4. Create Top-Right [?] Help Toggle Button
            GameObject helpBtnObj = new GameObject("HelpToggleButton");
            helpBtnObj.transform.SetParent(canvas.transform, false);
            RectTransform helpRect = helpBtnObj.AddComponent<RectTransform>();
            helpRect.anchorMin = new Vector2(1f, 1f);
            helpRect.anchorMax = new Vector2(1f, 1f);
            helpRect.pivot = new Vector2(1f, 1f);
            helpRect.anchoredPosition = new Vector2(-20f, -20f);
            helpRect.sizeDelta = new Vector2(50f, 50f);

            Image helpBg = helpBtnObj.AddComponent<Image>();
            helpBg.color = new Color(0.12f, 0.15f, 0.2f, 0.85f);

            helpButton = helpBtnObj.AddComponent<Button>();
            helpButton.onClick.AddListener(ToggleTutorial);

            GameObject helpTextObj = new GameObject("HelpIcon");
            helpTextObj.transform.SetParent(helpBtnObj.transform, false);
            RectTransform helpTextRect = helpTextObj.AddComponent<RectTransform>();
            helpTextRect.anchorMin = Vector2.zero;
            helpTextRect.anchorMax = Vector2.one;
            helpTextRect.offsetMin = Vector2.zero;
            helpTextRect.offsetMax = Vector2.zero;

            Text helpText = helpTextObj.AddComponent<Text>();
            helpText.font = tutorialText.font;
            helpText.fontSize = 26;
            helpText.fontStyle = FontStyle.Bold;
            helpText.alignment = TextAnchor.MiddleCenter;
            helpText.color = Color.white;
            helpText.text = "?";

            UpdateTutorialText();
        }
    }
}
