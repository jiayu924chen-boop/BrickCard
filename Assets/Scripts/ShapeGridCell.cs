using UnityEngine;
using UnityEngine.UI;

namespace CardGame.Editors
{
    [RequireComponent(typeof(Image))]
    public sealed class ShapeGridCell : MonoBehaviour
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private Color emptyColor = new Color(0.82f, 0.87f, 0.93f, 1f);
        [SerializeField] private Color filledColor = new Color(0.21f, 0.58f, 0.86f, 1f);

        private bool isFilled;

        public void Initialize()
        {
            if (targetImage == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                for (var i = 0; i < images.Length; i++)
                {
                    if (images[i].gameObject != gameObject)
                    {
                        targetImage = images[i];
                        break;
                    }
                }

                if (targetImage == null)
                {
                    targetImage = GetComponent<Image>();
                }
            }

            var rootImage = GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.raycastTarget = false;
            }

            targetImage.raycastTarget = false;
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
    }
}
