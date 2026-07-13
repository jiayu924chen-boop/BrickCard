using CardGame.Foundation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CardGame.EditorTools
{
    public static class CardGameSceneBuilder
    {
        private const string StartScenePath = "Assets/Scenes/StartScene.unity";
        private const string BattleScenePath = "Assets/Scenes/BattleScene.unity";
        private const string BoardCellPrefabPath = "Assets/Prefabs/BoardCell.prefab";

        [MenuItem("Card Game/Build Foundation Scenes")]
        public static void BuildFoundationScenes()
        {
            ConfigureProject();
            BuildBoardCellPrefab();
            BuildStartScene();
            BuildBattleScene();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureProject()
        {
            PlayerSettings.companyName = "DefaultCompany";
            PlayerSettings.productName = "Card Game";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetAspectRatio(AspectRatio.Aspect16by9, true);
            PlayerSettings.SetAspectRatio(AspectRatio.Aspect16by10, true);
            PlayerSettings.SetAspectRatio(AspectRatio.AspectOthers, true);
        }

        private static void BuildBoardCellPrefab()
        {
            var cell = CreateUiObject("BoardCell", null);
            var image = cell.AddComponent<Image>();
            image.color = new Color(0.89f, 0.92f, 0.96f, 1f);

            var outline = cell.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.22f, 0.30f, 0.65f);
            outline.effectDistance = new Vector2(2f, -2f);

            var layout = cell.AddComponent<LayoutElement>();
            layout.preferredWidth = 86f;
            layout.preferredHeight = 86f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            PrefabUtility.SaveAsPrefabAsset(cell, BoardCellPrefabPath);
            Object.DestroyImmediate(cell);
        }

        private static void BuildStartScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "StartScene";

            CreateCamera("Main Camera", new Color(0.07f, 0.09f, 0.12f, 1f));
            CreateEventSystem();

            var canvas = CreateCanvas("Start Canvas");
            CreateFullScreenPanel("Background", canvas.transform, new Color(0.08f, 0.11f, 0.15f, 1f));

            var title = CreateText("Title", canvas.transform, "Card Game", 88, TextAnchor.MiddleCenter, Color.white);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 140f), new Vector2(0f, -170f));

            var subtitle = CreateText("Subtitle", canvas.transform, "Foundation scene for future card battle design", 28, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.92f, 1f));
            SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 60f), new Vector2(0f, -260f));

            var panel = CreateUiObject("Main Menu Button Group", canvas.transform);
            var panelRect = panel.GetComponent<RectTransform>();
            SetRect(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 300f), new Vector2(0f, -30f));

            var vertical = panel.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 26f;
            vertical.childAlignment = TextAnchor.MiddleCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            CreateMenuButton(panel.transform, "Start Game Button", "开始游戏", SceneButtonAction.ButtonAction.LoadScene, "BattleScene", "Load battle scene");
            CreateMenuButton(panel.transform, "Level Select Button", "选择关卡", SceneButtonAction.ButtonAction.Placeholder, string.Empty, "Level select is reserved for later implementation.");
            CreateMenuButton(panel.transform, "Settings Button", "设置", SceneButtonAction.ButtonAction.Placeholder, string.Empty, "Settings are reserved for later implementation.");

            CreatePlaceholderPanel(canvas.transform, "Level Select Placeholder Panel", "Reserved level selection panel");
            CreatePlaceholderPanel(canvas.transform, "Settings Placeholder Panel", "Reserved settings panel");

            EditorSceneManager.SaveScene(scene, StartScenePath);
        }

        private static void BuildBattleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "BattleScene";

            CreateCamera("Main Camera", new Color(0.06f, 0.08f, 0.10f, 1f));
            CreateEventSystem();

            var canvas = CreateCanvas("Battle Canvas");
            CreateFullScreenPanel("Battle Background", canvas.transform, new Color(0.06f, 0.08f, 0.10f, 1f));

            var header = CreateFullWidthPanel("Battle Header", canvas.transform, new Color(0.10f, 0.14f, 0.18f, 0.95f), 104f, true);
            CreateText("Battle Title", header.transform, "Battle", 42, TextAnchor.MiddleCenter, Color.white);

            var backButton = CreateMenuButton(header.transform, "Back To Start Button", "返回", SceneButtonAction.ButtonAction.LoadScene, "StartScene", "Return to start scene");
            SetRect(backButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 58f), new Vector2(110f, 0f));

            var infoText = CreateText("Battle Info Text", canvas.transform, "Battle area placeholder", 26, TextAnchor.MiddleCenter, new Color(0.72f, 0.80f, 0.88f, 1f));
            SetRect(infoText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(600f, 50f), new Vector2(0f, -150f));

            var boardRoot = CreateUiObject("Board 10x8 Root", canvas.transform);
            var boardRootRect = boardRoot.GetComponent<RectTransform>();
            SetRect(boardRootRect, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f), new Vector2(1000f, 808f), Vector2.zero);

            var boardBackground = boardRoot.AddComponent<Image>();
            boardBackground.color = new Color(0.13f, 0.17f, 0.21f, 0.98f);
            var boardOutline = boardRoot.AddComponent<Outline>();
            boardOutline.effectColor = new Color(0.02f, 0.03f, 0.04f, 0.8f);
            boardOutline.effectDistance = new Vector2(3f, -3f);

            var grid = CreateUiObject("Grid Layout 10 Columns x 8 Rows", boardRoot.transform);
            var gridRect = grid.GetComponent<RectTransform>();
            SetRect(gridRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(932f, 744f), Vector2.zero);

            var gridLayout = grid.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(86f, 86f);
            gridLayout.spacing = new Vector2(8f, 8f);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 10;

            var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoardCellPrefabPath);
            for (var row = 0; row < 8; row++)
            {
                for (var col = 0; col < 10; col++)
                {
                    var cell = (GameObject)PrefabUtility.InstantiatePrefab(cellPrefab, grid.transform);
                    cell.name = $"Cell_R{row + 1:00}_C{col + 1:00}";
                    var image = cell.GetComponent<Image>();
                    image.color = (row + col) % 2 == 0
                        ? new Color(0.86f, 0.90f, 0.95f, 1f)
                        : new Color(0.76f, 0.82f, 0.89f, 1f);
                }
            }

            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        private static Canvas CreateCanvas(string name)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static GameObject CreateCamera(string name, Color background)
        {
            var cameraObject = new GameObject(name, typeof(Camera), typeof(AudioListener));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            cameraObject.tag = "MainCamera";
            return cameraObject;
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }
            return gameObject;
        }

        private static Image CreateFullScreenPanel(string name, Transform parent, Color color)
        {
            var panel = CreateUiObject(name, parent);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = panel.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Image CreateFullWidthPanel(string name, Transform parent, Color color, float height, bool top)
        {
            var panel = CreateUiObject(name, parent);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = top ? new Vector2(0f, 1f) : Vector2.zero;
            rect.anchorMax = top ? Vector2.one : new Vector2(1f, 0f);
            rect.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = Vector2.zero;
            var image = panel.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, Color color)
        {
            var textObject = CreateUiObject(name, parent);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = size;
            return text;
        }

        private static GameObject CreateMenuButton(Transform parent, string name, string label, SceneButtonAction.ButtonAction action, string sceneName, string placeholderMessage)
        {
            var buttonObject = CreateUiObject(name, parent);
            var rect = buttonObject.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 72f), Vector2.zero);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.46f, 0.78f, 1f);

            var button = buttonObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.22f, 0.46f, 0.78f, 1f);
            colors.highlightedColor = new Color(0.30f, 0.56f, 0.88f, 1f);
            colors.pressedColor = new Color(0.14f, 0.32f, 0.58f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var actionComponent = buttonObject.AddComponent<SceneButtonAction>();
            var serializedObject = new SerializedObject(actionComponent);
            serializedObject.FindProperty("action").enumValueIndex = (int)action;
            serializedObject.FindProperty("sceneName").stringValue = sceneName;
            serializedObject.FindProperty("placeholderMessage").stringValue = placeholderMessage;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(button.onClick, actionComponent.Execute);

            var labelText = CreateText("Label", buttonObject.transform, label, 30, TextAnchor.MiddleCenter, Color.white);
            labelText.raycastTarget = false;
            return buttonObject;
        }

        private static void CreatePlaceholderPanel(Transform parent, string name, string label)
        {
            var panel = CreateUiObject(name, parent);
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760f, 420f), Vector2.zero);
            panel.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.18f, 0.96f);
            CreateText("Title", panel.transform, label, 36, TextAnchor.MiddleCenter, new Color(0.88f, 0.93f, 1f, 1f));
            panel.SetActive(false);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(StartScenePath, true),
                new EditorBuildSettingsScene(BattleScenePath, true)
            };
        }
    }
}
