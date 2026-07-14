using UnityEngine;
using UnityEngine.EventSystems;

namespace CardGame.Editors
{
    public sealed class CardPreviewHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CardEditorController controller;

        public void OnPointerEnter(PointerEventData eventData)
        {
            controller?.ShowCurrentCardPreview();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            controller?.HidePreview();
        }
    }
}
