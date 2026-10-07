using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PokoPond.UI.MainMenu
{
    /// <summary>
    /// Hover/selection visuals for a single menu row.
    /// Attach to each menu button. Moves the caret, tints the label, runs the
    /// caret blink while selected, and forwards hover to EventSystem.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public class MenuItemHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        [Header("References")]
        [SerializeField] private Image caret;
        [SerializeField] private TMP_Text label;
        [SerializeField] private RectTransform caretRect;

        [Header("Colors")]
        [SerializeField] private Color normalColor       = new Color32(0xB8, 0xA3, 0xD4, 0xFF); // lav-2
        [SerializeField] private Color hoverColor        = Color.white;
        [SerializeField] private Color caretActiveColor  = new Color32(0x7D, 0xD4, 0xE8, 0xFF); // droplet-core
        [SerializeField] private Color dangerHoverColor  = new Color32(0xE8, 0x7A, 0x8A, 0xFF); // danger

        [Header("Behaviour")]
        [SerializeField] private bool  isDanger       = false;
        [SerializeField] private float transitionTime = 0.15f;
        [SerializeField] private float caretSlideFrom = -10f;
        [SerializeField] private float caretBlinkRate = 0.9f;

        private Coroutine _transitionCo;
        private Coroutine _blinkCo;
        private bool      _isActive;
        private Selectable _selectable;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            // Ensure the default (deselected) visual state on startup.
            ApplyImmediate(false);
        }

        private void OnDisable()
        {
            StopAllTweens();
            ApplyImmediate(false);
        }

        // -------------------- event handlers --------------------

        public void OnPointerEnter(PointerEventData e)
        {
            if (!CanSelect()) return;
            // Route mouse hover through the EventSystem so keyboard nav stays in sync.
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(gameObject);
        }

        public void OnPointerExit(PointerEventData e)
        {
            // Do nothing: leaving the row does not necessarily deselect (keyboard may still own it).
        }

        public void OnSelect(BaseEventData e)    => SetActive(CanSelect());
        public void OnDeselect(BaseEventData e)  => SetActive(false);

        private bool CanSelect()
        {
            return _selectable != null && _selectable.IsActive() && _selectable.IsInteractable();
        }

        // -------------------- visual state --------------------

        private void SetActive(bool active)
        {
            if (_isActive == active) return;
            _isActive = active;

            StopAllTweens();
            _transitionCo = StartCoroutine(CoTransition(active));

            if (active)
            {
                if (caret != null) caret.enabled = true;
                _blinkCo = StartCoroutine(CoBlink());
            }
            else
            {
                if (caret != null)
                {
                    caret.enabled = false;
                    var c = caret.color; c.a = 0f; caret.color = c;
                }
            }
        }

        private void ApplyImmediate(bool active)
        {
            _isActive = active;
            if (label != null) label.color = active ? CurrentHover() : normalColor;
            if (caret != null)
            {
                caret.enabled = active;
                var c = active ? CurrentHover(onlyDanger: true) : caretActiveColor;
                c.a = active ? 1f : 0f;
                caret.color = c;
            }
            if (caretRect != null)
            {
                var p = caretRect.anchoredPosition;
                p.x = active ? 0f : caretSlideFrom;
                caretRect.anchoredPosition = p;
            }
        }

        private Color CurrentHover(bool onlyDanger = false)
        {
            if (isDanger) return dangerHoverColor;
            return onlyDanger ? caretActiveColor : hoverColor;
        }

        private IEnumerator CoTransition(bool toActive)
        {
            float t = 0f;
            Color labelFrom  = label != null ? label.color : normalColor;
            Color labelTo    = toActive ? CurrentHover() : normalColor;

            Color caretFrom  = caret != null ? caret.color : Color.clear;
            Color caretTarget = isDanger ? dangerHoverColor : caretActiveColor;
            Color caretTo    = toActive
                ? new Color(caretTarget.r, caretTarget.g, caretTarget.b, 1f)
                : new Color(caretTarget.r, caretTarget.g, caretTarget.b, 0f);

            Vector2 posFrom  = caretRect != null ? caretRect.anchoredPosition : Vector2.zero;
            Vector2 posTo    = toActive ? new Vector2(0f, posFrom.y) : new Vector2(caretSlideFrom, posFrom.y);

            float dur = Mathf.Max(0.01f, transitionTime);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                // ease out cubic
                float e = 1f - Mathf.Pow(1f - k, 3f);

                if (label != null) label.color = Color.LerpUnclamped(labelFrom, labelTo, e);
                if (caret != null) caret.color = Color.LerpUnclamped(caretFrom, caretTo, e);
                if (caretRect != null) caretRect.anchoredPosition = Vector2.LerpUnclamped(posFrom, posTo, e);
                yield return null;
            }

            if (label != null) label.color = labelTo;
            if (caret != null) caret.color = caretTo;
            if (caretRect != null) caretRect.anchoredPosition = posTo;
            _transitionCo = null;
        }

        private IEnumerator CoBlink()
        {
            // 2-step blink (hard on/off) every caretBlinkRate seconds, as spec'd.
            if (caret == null) yield break;
            while (_isActive)
            {
                var c = caret.color; c.a = 1f; caret.color = c;
                yield return new WaitForSecondsRealtime(caretBlinkRate * 0.5f);
                if (!_isActive) yield break;
                c = caret.color; c.a = 0.25f; caret.color = c;
                yield return new WaitForSecondsRealtime(caretBlinkRate * 0.5f);
            }
        }

        private void StopAllTweens()
        {
            if (_transitionCo != null) { StopCoroutine(_transitionCo); _transitionCo = null; }
            if (_blinkCo      != null) { StopCoroutine(_blinkCo);      _blinkCo      = null; }
        }

        // -------------------- public configuration --------------------

        public void Configure(Image caretImage, TMP_Text labelText, RectTransform caretRectTransform, bool danger)
        {
            caret      = caretImage;
            label      = labelText;
            caretRect  = caretRectTransform;
            isDanger   = danger;
            ApplyImmediate(false);
        }
    }
}
