using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardGame.Foundation
{
    public sealed class SceneButtonAction : MonoBehaviour
    {
        public enum ButtonAction
        {
            LoadScene,
            Placeholder,
            Quit
        }

        [SerializeField] private ButtonAction action = ButtonAction.Placeholder;
        [SerializeField] private string sceneName;
        [SerializeField] private string placeholderMessage = "This feature is reserved for later implementation.";

        public void Execute()
        {
            switch (action)
            {
                case ButtonAction.LoadScene:
                    if (!string.IsNullOrWhiteSpace(sceneName))
                    {
                        SceneManager.LoadScene(sceneName);
                    }
                    break;
                case ButtonAction.Quit:
                    Application.Quit();
                    break;
                default:
                    Debug.Log(placeholderMessage, this);
                    break;
            }
        }
    }
}
