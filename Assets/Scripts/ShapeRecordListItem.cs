using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardGame.Editors
{
    [RequireComponent(typeof(Button))]
    public sealed class ShapeRecordListItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Text label;
        [SerializeField] private Button targetButton;

        private Action clickAction;
        private Action hoverEnterAction;
        private Action hoverExitAction;

        public void Initialize(string textValue, Action onClick, Action onHoverEnter = null, Action onHoverExit = null)
        {
            if (label == null)
            {
                label = GetComponentInChildren<Text>(true);
            }

            if (targetButton == null)
            {
                targetButton = GetComponent<Button>();
            }

            label.text = textValue;
            clickAction = onClick;
            hoverEnterAction = onHoverEnter;
            hoverExitAction = onHoverExit;

            targetButton.onClick.RemoveAllListeners();
            targetButton.onClick.AddListener(HandleClick);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hoverEnterAction?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverExitAction?.Invoke();
        }

        private void HandleClick()
        {
            clickAction?.Invoke();
        }
    }
}
