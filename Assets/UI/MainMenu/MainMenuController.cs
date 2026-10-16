using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PokoPond.UI.MainMenu
{
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Buttons (order defines keyboard nav order)")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;

        [Header("Scene names")]
        [SerializeField] private string newGameScene = "Area1-1";

        [Header("Panels")]
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject creditsPanel;

        private readonly List<Button> _order = new List<Button>(5);
        private readonly List<Button> _navigable = new List<Button>(5);
        private readonly bool[] _interactableBeforePanel = new bool[5];
        private int _activeIndex;
        private GameObject _openPanel;
        private Button _selectionBeforePanel;

        private bool IsPanelOpen => _openPanel != null
            || (optionsPanel != null && optionsPanel.activeSelf)
            || (creditsPanel != null && creditsPanel.activeSelf);

        private void Awake()
        {
            RebuildOrder();
            DisableContinue();
            HookButtons();
        }

        private void OnEnable()
        {
            RebuildOrder();
            DisableContinue();
            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
            RebuildNavigation();
            _activeIndex = FirstInteractableIndex();
            SelectIndex(_activeIndex);
        }

        private void Start()
        {
            // The prefab's EventSystem may enable after this controller's OnEnable.
            SelectIndex(_activeIndex);
        }

        private void OnDisable()
        {
            ClosePanel();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) ClosePanel();
        }

        private void LateUpdate()
        {
            if (IsPanelOpen) return;
            var es = EventSystem.current;
            if (es == null) return;

            var selected = es.currentSelectedGameObject;
            for (int i = 0; i < _order.Count; i++)
            {
                if (_order[i] != null && _order[i].gameObject == selected)
                {
                    if (CanSelect(_order[i])) { _activeIndex = i; return; }
                    selected = null;
                    break;
                }
            }

            // Pointer clicks on the background clear focus during EventSystem.Update.
            if (selected == null)
            {
                if (_activeIndex < 0 || _activeIndex >= _order.Count || !CanSelect(_order[_activeIndex]))
                    _activeIndex = FirstInteractableIndex();
                SelectIndex(_activeIndex);
            }
        }

        private void DisableContinue()
        {
            // There is no save restoration yet, so never offer a fresh game as Continue.
            if (continueButton == null) return;
            continueButton.interactable = false;
            continueButton.gameObject.SetActive(false);
        }

        private void RebuildOrder()
        {
            _order.Clear();
            if (continueButton != null) _order.Add(continueButton);
            if (newGameButton != null) _order.Add(newGameButton);
            if (optionsButton != null) _order.Add(optionsButton);
            if (creditsButton != null) _order.Add(creditsButton);
            if (quitButton != null) _order.Add(quitButton);
        }

        private void RebuildNavigation()
        {
            _navigable.Clear();
            foreach (var button in _order)
                if (CanSelect(button)) _navigable.Add(button);

            foreach (var button in _order)
            {
                if (button == null) continue;
                var navigation = new Navigation { mode = Navigation.Mode.None };
                int i = _navigable.IndexOf(button);
                if (i >= 0)
                {
                    navigation.mode = Navigation.Mode.Explicit;
                    navigation.selectOnUp = _navigable[(i + _navigable.Count - 1) % _navigable.Count];
                    navigation.selectOnDown = _navigable[(i + 1) % _navigable.Count];
                }
                button.navigation = navigation;
            }
        }

        private void HookButtons()
        {
            if (continueButton != null) { continueButton.onClick.RemoveListener(OnContinue); continueButton.onClick.AddListener(OnContinue); }
            if (newGameButton != null) { newGameButton.onClick.RemoveListener(OnNewGame); newGameButton.onClick.AddListener(OnNewGame); }
            if (optionsButton != null) { optionsButton.onClick.RemoveListener(OnOptions); optionsButton.onClick.AddListener(OnOptions); }
            if (creditsButton != null) { creditsButton.onClick.RemoveListener(OnCredits); creditsButton.onClick.AddListener(OnCredits); }
            if (quitButton != null) { quitButton.onClick.RemoveListener(OnQuit); quitButton.onClick.AddListener(OnQuit); }
        }

        private static bool CanSelect(Button button)
        {
            return button != null && button.IsActive() && button.IsInteractable();
        }

        private void SelectIndex(int idx)
        {
            if (IsPanelOpen || idx < 0 || idx >= _order.Count || !CanSelect(_order[idx])) return;
            _activeIndex = idx;
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(_order[idx].gameObject);
        }

        private int FirstInteractableIndex()
        {
            for (int i = 0; i < _order.Count; i++)
                if (CanSelect(_order[i])) return i;
            return -1;
        }

        public void OnContinue()
        {
            // Retained for existing UnityEvent bindings until saves can be restored.
        }

        public void OnNewGame()
        {
            if (!isActiveAndEnabled || IsPanelOpen) return;
            LoadSceneSafe(newGameScene, "New Game");
        }

        public void OnOptions()
        {
            OpenPanel(optionsPanel, optionsButton, "options");
        }

        public void OnCredits()
        {
            OpenPanel(creditsPanel, creditsButton, "credits");
        }

        private void OpenPanel(GameObject panel, Button opener, string label)
        {
            if (!isActiveAndEnabled || IsPanelOpen) return;
            if (panel == null) { Debug.LogWarning($"[MainMenu] No {label} panel wired up."); return; }

            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            _selectionBeforePanel = opener;
            for (int i = 0; i < _order.Count; i++)
            {
                var button = _order[i];
                _interactableBeforePanel[i] = button != null && button.interactable;
                if (CanSelect(button) && button.gameObject == selected) _selectionBeforePanel = button;
            }

            _openPanel = panel;
            if (es != null) es.SetSelectedGameObject(null);
            foreach (var button in _order)
                if (button != null) button.interactable = false;
            RebuildNavigation();
            panel.SetActive(true);
        }

        private void ClosePanel()
        {
            if (_openPanel == null) return;
            _openPanel.SetActive(false);
            _openPanel = null;
            for (int i = 0; i < _order.Count; i++)
                if (_order[i] != null) _order[i].interactable = _interactableBeforePanel[i];
            RebuildNavigation();
            int index = _order.IndexOf(_selectionBeforePanel);
            SelectIndex(index >= 0 && CanSelect(_selectionBeforePanel) ? index : FirstInteractableIndex());
            _selectionBeforePanel = null;
        }

        public void OnQuit()
        {
            if (!isActiveAndEnabled || IsPanelOpen) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void LoadSceneSafe(string sceneName, string actionLabel)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning($"[MainMenu] {actionLabel} pressed but no scene name is set.");
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[MainMenu] Scene '{sceneName}' is not in the Build Settings scene list.");
                return;
            }
            SceneManager.LoadScene(sceneName);
        }
    }
}
