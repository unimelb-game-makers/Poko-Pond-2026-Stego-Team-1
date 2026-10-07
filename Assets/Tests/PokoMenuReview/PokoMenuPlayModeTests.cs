using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PokoMenuReview
{
    // Reflection lets these tests exercise the real Assembly-CSharp components
    // without changing the production assembly layout.
    public class PokoMenuPlayModeTests
    {
        private Component controller;
        private EventSystem events;
        private Button newGame;
        private Button continueButton;
        private Button options;
        private Button credits;
        private Button quit;
        private GameObject optionsPanel;
        private GameObject creditsPanel;
        private Button[] menuButtons;

        [UnitySetUp]
        public IEnumerator OpenRealStartMenu()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("StartMenu", LoadSceneMode.Single);
            controller = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(component => component.GetType().FullName ==
                    "PokoPond.UI.MainMenu.MainMenuController");
            Assert.That(controller, Is.Not.Null, "StartMenu must instantiate its real menu controller.");
            newGame = Field<Button>("newGameButton");
            continueButton = Field<Button>("continueButton");
            options = Field<Button>("optionsButton");
            credits = Field<Button>("creditsButton");
            quit = Field<Button>("quitButton");
            optionsPanel = Field<GameObject>("optionsPanel");
            creditsPanel = Field<GameObject>("creditsPanel");
            menuButtons = new[] { continueButton, newGame, options, credits, quit };
            Assert.That(menuButtons.All(button => button != null), Is.True, "Every menu button must be wired.");
            events = EventSystem.current;
            Assert.That(events, Is.Not.Null, "StartMenu must create an active EventSystem.");
            // Prevent the physical mouse in the editor from making these tests flaky.
            var module = events.GetComponent<StandaloneInputModule>();
            Assert.That(module, Is.Not.Null);
            module.inputOverride = events.gameObject.AddComponent<QuietInput>();
            yield return null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartupFocusAndNavigationSkipUnavailableContinue()
        {
            Assert.That(continueButton.IsActive() && continueButton.IsInteractable(), Is.False,
                "Continue must not advertise a playable action without save restoration.");
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(newGame.gameObject),
                "New Game must be selected after EventSystem startup.");
            foreach (var expected in new[] { options, credits, quit, newGame })
            {
                Move(MoveDirection.Down);
                Assert.That(events.currentSelectedGameObject, Is.EqualTo(expected.gameObject),
                    "Down navigation must wrap through available buttons only.");
            }
            Move(MoveDirection.Up);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(quit.gameObject),
                "Up from New Game must wrap to Quit, bypassing unavailable Continue.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearingSelectionRecoversUsableKeyboardFocus()
        {
            events.SetSelectedGameObject(options.gameObject);
            yield return null;
            // This is the EventSystem state after clicking a raycastable background.
            events.SetSelectedGameObject(null);
            yield return null;
            yield return null;
            var recovered = events.currentSelectedGameObject;
            Assert.That(recovered, Is.Not.Null, "A blank click must not permanently strand keyboard users.");
            var selectedButton = recovered.GetComponent<Button>();
            Assert.That(selectedButton != null && selectedButton.IsActive() && selectedButton.IsInteractable(),
                Is.True, "Recovered focus must be an available button.");
            Move(MoveDirection.Down);
            Assert.That(events.currentSelectedGameObject, Is.Not.Null);
            Assert.That(events.currentSelectedGameObject, Is.Not.EqualTo(recovered),
                "The recovered selection must accept navigation.");
        }

        [UnityTest]
        public IEnumerator UnavailableContinueCannotLoadASceneOrStealHoverFocus()
        {
            events.SetSelectedGameObject(newGame.gameObject);
            ExecuteEvents.Execute(continueButton.gameObject, new PointerEventData(events),
                ExecuteEvents.pointerEnterHandler);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(newGame.gameObject),
                "Unavailable Continue must not steal hover focus.");
            Call("OnContinue");
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("StartMenu"),
                "Even a stale Continue callback must be harmless without saved progress.");
        }

        [UnityTest]
        public IEnumerator OptionsBlocksUnderlyingActionsAndRestoresFocus()
        {
            yield return CheckModal(options, optionsPanel, creditsPanel);
        }

        [UnityTest]
        public IEnumerator CreditsBlocksUnderlyingActionsAndRestoresFocus()
        {
            yield return CheckModal(credits, creditsPanel, optionsPanel);
        }

        [UnityTest]
        public IEnumerator ClosingPanelPreservesPreviouslyDisabledButtons()
        {
            quit.interactable = false;
            events.SetSelectedGameObject(options.gameObject);
            ExecuteEvents.Execute(options.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            yield return null;
            Assert.That(optionsPanel.activeInHierarchy, Is.True);
            Call("ClosePanel");
            yield return null;
            Assert.That(quit.IsInteractable(), Is.False,
                "Modal close must restore each button's previous availability.");
            Assert.That(newGame.IsInteractable(), Is.True);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(options.gameObject));
            Move(MoveDirection.Down);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(credits.gameObject));
            Move(MoveDirection.Down);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(newGame.gameObject),
                "Rebuilt navigation must bypass buttons that remain disabled.");
        }

        [UnityTest]
        public IEnumerator NewGameButtonLoadsArea1_1()
        {
            Assert.That(Field<string>("newGameScene"), Is.EqualTo("Area1-1"),
                "The current upstream first stage is Area1-1.");
            Assert.That(Application.CanStreamedLevelBeLoaded("Area1-1"), Is.True,
                "Area1-1 must be included and enabled in the build scene list.");
            events.SetSelectedGameObject(newGame.gameObject);
            ExecuteEvents.Execute(newGame.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            var deadline = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().name == "StartMenu" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Area1-1"),
                "Submitting the wired New Game button must load the real starting stage.");
            yield return null;
            yield return null;
        }

        private IEnumerator CheckModal(Button opener, GameObject panel, GameObject otherPanel)
        {
            Assert.That(panel, Is.Not.Null);
            events.SetSelectedGameObject(opener.gameObject);
            ExecuteEvents.Execute(opener.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            yield return null;
            Assert.That(panel.activeInHierarchy, Is.True, "The wired button must open its panel.");
            Assert.That(otherPanel.activeSelf, Is.False, "Only one panel should be open.");
            foreach (var button in menuButtons)
                Assert.That(button.IsActive() && button.IsInteractable(), Is.False,
                    button.name + " must not accept actions behind a modal panel.");
            Assert.That(menuButtons.Any(button => button.gameObject == events.currentSelectedGameObject),
                Is.False, "The panel must clear or transfer focus away from the hidden menu.");
            ExecuteEvents.Execute(newGame.gameObject, new PointerEventData(events),
                ExecuteEvents.pointerEnterHandler);
            Assert.That(menuButtons.Any(button => button.gameObject == events.currentSelectedGameObject),
                Is.False, "Hover handlers on disabled underlying buttons must not steal modal focus.");

            // Real uGUI move/submit event dispatch while the panel is open.
            for (var i = 0; i < 8; i++)
            {
                Move(MoveDirection.Down);
                if (events.currentSelectedGameObject != null)
                    ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events),
                        ExecuteEvents.submitHandler);
            }
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("StartMenu"));
            Assert.That(panel.activeInHierarchy, Is.True);
            // A stale serialized/event callback must also respect the modal gate.
            Call("OnNewGame");
            Call("OnContinue");
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("StartMenu"),
                "Direct menu callbacks must not change scenes behind a panel.");

            // Escape delegates to this method. Physical key injection is deliberately
            // outside this harness; this checks the same close/focus-restoration path.
            Call("ClosePanel");
            yield return null;
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(opener.gameObject),
                "Closing the panel must return keyboard focus to its opener.");
            foreach (var button in new[] { newGame, options, credits, quit })
                Assert.That(button.IsActive() && button.IsInteractable(), Is.True,
                    button.name + " must become usable after closing the panel.");
            Assert.That(continueButton.IsActive() && continueButton.IsInteractable(), Is.False,
                "Closing a panel must not accidentally unlock Continue.");
            Move(MoveDirection.Down);
            Assert.That(events.currentSelectedGameObject, Is.Not.Null);
            Assert.That(events.currentSelectedGameObject, Is.Not.EqualTo(continueButton.gameObject));
        }

        private T Field<T>(string name)
        {
            var field = controller.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing expected serialized field: " + name);
            return (T)field.GetValue(controller);
        }

        private void Call(string name)
        {
            var method = controller.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing expected controller action: " + name);
            method.Invoke(controller, null);
        }

        private void Move(MoveDirection direction)
        {
            if (events.currentSelectedGameObject == null) return;
            var data = new AxisEventData(events) { moveDir = direction,
                moveVector = direction == MoveDirection.Up ? Vector2.up : Vector2.down };
            ExecuteEvents.Execute(events.currentSelectedGameObject, data, ExecuteEvents.moveHandler);
        }
    }

    public sealed class QuietInput : BaseInput
    {
        public override bool mousePresent => false;
        public override bool touchSupported => false;
        public override int touchCount => 0;
        public override Vector2 mousePosition => Vector2.zero;
        public override Vector2 mouseScrollDelta => Vector2.zero;
        public override bool GetMouseButtonDown(int button) => false;
        public override bool GetMouseButtonUp(int button) => false;
        public override bool GetMouseButton(int button) => false;
        public override float GetAxisRaw(string axisName) => 0f;
        public override bool GetButtonDown(string buttonName) => false;
    }
}
