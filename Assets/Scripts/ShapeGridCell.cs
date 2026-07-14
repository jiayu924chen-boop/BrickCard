using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardGame.Editors
{
    [RequireComponent(typeof(Image))]
    public sealed class ShapeGridCell : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private Color emptyColor = new Color(0.82f, 0.87f, 0.93f, 1f);
        [SerializeField] private Color filledColor = new Color(0.21f, 0.58f, 0.86f, 1f);

        private ShapeGridEditor owner;
        private bool isFilled;
        private int x;
        private int y;

        public void Initialize(ShapeGridEditor editor, int cellX, int cellY)
        {
            owner = editor;
            x = cellX;
            y = cellY;

            if (targetImage == null)
            {
                targetImage = GetComponent<Image>();
            }

            SetFilled(false);
        }

        public void SetFilled(bool value)
        {
            isFilled = value;
            if (targetImage != null)
            {
                targetImage.color = isFilled ? filledColor : emptyColor;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (owner == null || !owner.Editable)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                owner.BeginPaint(true);
                owner.ApplyPaint(x, y);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                owner.EraseSingleCell(x, y);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner == null)
            {
                return;
            }

            owner.SetHoveredCell(x, y, true);
            if (owner.Editable && owner.IsPainting && Input.GetMouseButton(0))
            {
                owner.ApplyPaint(x, y);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.SetHoveredCell(x, y, false);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (owner == null)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                owner.EndPaint();
            }
        }
    }
}
