using System.IO;
using CardGame.Editors;
using CardGame.Foundation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardGame.EditorTools
{
    public static class CardGameSceneBuilder
    {
        private const string StartScenePath = "Assets/Scenes/StartScene.unity";
        private const string BattleScenePath = "Assets/Scenes/BattleScene.unity";
        private const string BoardEditorScenePath = "Assets/Scenes/BoardEditorScene.unity";
        private const string CardEditorScenePath = "Assets/Scenes/CardEditorScene.unity";
        private const string BoardCellPrefabPath = "Assets/Prefabs/BoardCell.prefab";
        private const string EditorGridCellPrefabPath = "Assets/Prefabs/EditorGridCell.prefab";
        private const string ListItemPrefabPath = "Assets/Prefabs/RecordListItem.prefab";

        [MenuItem("Card Game/Build Foundation Scenes")]
        public static void BuildFoundationScenes()
        {
            ConfigureProject();
            EnsureDataFiles();
            BuildBoardCellPrefab();
            BuildEditorGridCellPrefab();
            BuildRecordListItemPrefab();
            BuildStartScene();
            BuildBattleScene();
            BuildBoardEditorScene();
            BuildCardEditorScene();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Card Game/Validate Foundation Scenes")]
        public static void ValidateFoundationScenes()
        {
            ValidateEditorScene<BoardEditorController>(
                BoardEditorScenePath,
                "gridEditor", "boardIdInput", "columnsInput", "rowsInput", "statusText", "statisticsText", "listRoot", "listItemPrefab");
            ValidateEditorScene<CardEditorController>(
                CardEditorScenePath,
                "gridEditor", "cardIdInput", "columnsInput", "rowsInput", "nameInput", "effectInput", "descriptionInput", "notesInput",
                "modeText", "statusText", "statisticsText", "previewTitleText", "previewShapeText", "previewEffectText",
                "previewDescriptionText", "previewNotesText", "previewPanel", "listRoot", "listItemPrefab");

            var configuredScenePaths = EditorBuildSettings.scenes;
            var expectedPaths = new[] { StartScenePath, BattleScenePath, BoardEditorScenePath, CardEditorScenePath };
            for (var i = 0; i < expectedPaths.Length; i++)
            {
                var expectedPath = expectedPaths[i];
                var found = false;
                for (var sceneIndex = 0; sceneIndex < configuredScenePaths.Length; sceneIndex++)
                {
                    if (configuredScenePaths[sceneIndex].enabled && configuredScenePaths[sceneIndex].path == expectedPath)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    throw new BuildFailedException($"Build Settings is missing enabled scene: {expectedPath}");
                }
            }

            ValidateJsonRoundTrip();
            Debug.Log("Card Game validation passed: scenes, component references, button bindings, build settings, and JSON round-trip are valid.");
        }

        private static void ConfigureProject()
        {
            PlayerSettings.companyName = "DefaultCompany";
            PlayerSettings.productName = "Card Game";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
        }

        private static void EnsureDataFiles()
        {
            ShapeEditorPaths.EnsureDirectories();
            if (!File.Exists(ShapeEditorPaths.BoardDatabasePath))
            {
                File.WriteAllText(ShapeEditorPaths.BoardDatabasePath, JsonUtility.ToJson(new BoardShapeDatabase(), true));
            }
        }

        private static void BuildBoardCellPrefab()
        {
            var cell = CreateUiObject("BoardCell", null);
            var frame = cell.AddComponent<Image>();
            frame.color = new Color(0.18f, 0.22f, 0.28f, 1f);

            var fill = CreateUiObject("Fill", cell.transform);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.89f, 0.92f, 0.96f, 1f);
            SetRect(fill.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-2f, -2f), Vector2.zero);

            var layout = cell.AddComponent<LayoutElement>();
            layout.preferredWidth = 86f;
            layout.preferredHeight = 86f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            PrefabUtility.SaveAsPrefabAsset(cell, BoardCellPrefabPath);
            Object.DestroyImmediate(cell);
        }

        private static void BuildEditorGridCellPrefab()
        {
            var cell = CreateUiObject("EditorGridCell", null);
            var frame = cell.AddComponent<Image>();
            frame.color = new Color(0.18f, 0.22f, 0.28f, 1f);

            var fill = CreateUiObject("Fill", cell.transform);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.82f, 0.87f, 0.93f, 1f);
            SetRect(fill.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-2f, -2f), Vector2.zero);

            var shapeCell = cell.AddComponent<ShapeGridCell>();
            var serializedObject = new SerializedObject(shapeCell);
            serializedObject.FindProperty("targetImage").objectReferenceValue = fillImage;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(cell, EditorGridCellPrefabPath);
            Object.DestroyImmediate(cell);
        }

        private static void BuildRecordListItemPrefab()
        {
            var item = CreateUiObject("RecordListItem", null);
            SetRect(item.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 44f), Vector2.zero);

            var image = item.AddComponent<Image>();
            image.color = new Color(0.18f, 0.24f, 0.31f, 0.96f);

            var button = item.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.25f, 0.34f, 0.43f, 1f);
            colors.pressedColor = new Color(0.14f, 0.19f, 0.26f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var label = CreateText("Label", item.transform, "Record", 20, TextAnchor.MiddleLeft, Color.white);
            label.raycastTarget = false;
            SetRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-24f, 0f), new Vector2(0f, 0f));

            var listItem = item.AddComponent<ShapeRecordListItem>();
            var serializedObject = new SerializedObject(listItem);
            serializedObject.FindProperty("label").objectReferenceValue = label;
            serializedObject.FindProperty("targetButton").objectReferenceValue = button;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(item, ListItemPrefabPath);
            Object.DestroyImmediate(item);
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

            var subtitle = CreateText("Subtitle", canvas.transform, "Foundation scene for battle and content editing", 28, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.92f, 1f));
            SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(980f, 60f), new Vector2(0f, -260f));

            var panel = CreateUiObject("Main Menu Button Group", canvas.transform);
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 470f), new Vector2(0f, 45f));

            var vertical = panel.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 18f;
            vertical.childAlignment = TextAnchor.MiddleCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = true;

            CreateMenuButton(panel.transform, "Start Game Button", "Start Game", SceneButtonAction.ButtonAction.LoadScene, "BattleScene", "Load battle scene");
            CreateMenuButton(panel.transform, "Board Editor Button", "Board Editor", SceneButtonAction.ButtonAction.LoadScene, "BoardEditorScene", "Open board editor");
            CreateMenuButton(panel.transform, "Card Editor Button", "Card Editor", SceneButtonAction.ButtonAction.LoadScene, "CardEditorScene", "Open card editor");
            CreateMenuButton(panel.transform, "Level Select Button", "Level Select", SceneButtonAction.ButtonAction.Placeholder, string.Empty, "Level select is reserved for later implementation.");
            CreateMenuButton(panel.transform, "Settings Button", "Settings", SceneButtonAction.ButtonAction.Placeholder, string.Empty, "Settings are reserved for later implementation.");

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

            var backButton = CreateMenuButton(header.transform, "Back To Start Button", "Back", SceneButtonAction.ButtonAction.LoadScene, "StartScene", "Return to start scene");
            SetRect(backButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 58f), new Vector2(110f, 0f));

            var infoText = CreateText("Battle Info Text", canvas.transform, "Battle area placeholder", 26, TextAnchor.MiddleCenter, new Color(0.72f, 0.80f, 0.88f, 1f));
            SetRect(infoText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(600f, 50f), new Vector2(0f, -150f));

            var boardRoot = CreateUiObject("Board 10x8 Root", canvas.transform);
            SetRect(boardRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f), new Vector2(1000f, 808f), Vector2.zero);

            var boardBackground = boardRoot.AddComponent<Image>();
            boardBackground.color = new Color(0.13f, 0.17f, 0.21f, 0.98f);
            var boardOutline = boardRoot.AddComponent<Outline>();
            boardOutline.effectColor = new Color(0.02f, 0.03f, 0.04f, 0.8f);
            boardOutline.effectDistance = new Vector2(3f, -3f);

            var grid = CreateUiObject("Grid Layout 10 Columns x 8 Rows", boardRoot.transform);
            SetRect(grid.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(932f, 744f), Vector2.zero);

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

        private static void BuildBoardEditorScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "BoardEditorScene";

            CreateCamera("Main Camera", new Color(0.08f, 0.09f, 0.11f, 1f));
            CreateEventSystem();

            var canvas = CreateCanvas("Board Editor Canvas");
            CreateFullScreenPanel("Background", canvas.transform, new Color(0.08f, 0.09f, 0.11f, 1f));

            var header = CreateFullWidthPanel("Header", canvas.transform, new Color(0.12f, 0.15f, 0.19f, 0.98f), 94f, true);
            CreateText("Header Title", header.transform, "Board Editor", 38, TextAnchor.MiddleCenter, Color.white);
            var backButton = CreateMenuButton(header.transform, "Back Button", "Home", SceneButtonAction.ButtonAction.LoadScene, "StartScene", "Return to start scene");
            SetRect(backButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(140f, 52f), new Vector2(100f, 0f));

            var sidebar = CreatePanel("Sidebar", canvas.transform, new Color(0.11f, 0.14f, 0.18f, 0.98f));
            SetRect(sidebar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(360f, -122f), new Vector2(0f, -108f));

            var sidebarContent = CreateVerticalPanelContent("Sidebar Content", sidebar.transform, 16f, new RectOffset(18, 18, 18, 18));
            sidebarContent.childForceExpandHeight = false;

            CreateTextBlock("Board Id Label", sidebarContent.transform, "Board ID", 22, 36f);
            var boardIdInput = CreateInputField("Board Id Input", sidebarContent.transform, "1", 48f, false);

            CreateTextBlock("Board Size Label", sidebarContent.transform, "Grid Size (1-99)", 22, 36f);
            var sizeRow = CreateHorizontalPanelContent("Size Row", sidebarContent.transform, 10f, 48f);
            var columnsInput = CreateInputField("Columns Input", sizeRow.transform, "10", 48f, false);
            var rowsInput = CreateInputField("Rows Input", sizeRow.transform, "8", 48f, false);
            var applySizeButton = CreateActionButton(sizeRow.transform, "Apply Size Button", "Apply");
            StretchButton(columnsInput.gameObject);
            StretchButton(rowsInput.gameObject);
            StretchButton(applySizeButton);

            var buttonRow = CreateHorizontalPanelContent("Action Row", sidebarContent.transform, 10f, 52f);
            var newButton = CreateActionButton(buttonRow.transform, "New Board Button", "New");
            var loadButton = CreateActionButton(buttonRow.transform, "Load Board Button", "Load");
            var saveButton = CreateActionButton(buttonRow.transform, "Save Board Button", "Save");
            var deleteButton = CreateActionButton(buttonRow.transform, "Delete Board Button", "Delete");
            StretchButton(newButton);
            StretchButton(loadButton);
            StretchButton(saveButton);
            StretchButton(deleteButton);

            var statusText = CreateTextBlock("Status Text", sidebarContent.transform, "Ready.", 18, 56f);
            statusText.alignment = TextAnchor.MiddleLeft;
            var statisticsText = CreateTextBlock("Statistics Text", sidebarContent.transform, "10 x 8 | 0 cells", 16, 36f);
            statisticsText.alignment = TextAnchor.MiddleLeft;

            CreateTextBlock("Board List Label", sidebarContent.transform, "Saved Boards", 22, 36f);
            CreateScrollList("Board List", sidebarContent.transform, out var boardListContent, 0f);

            var workspace = CreatePanel("Workspace", canvas.transform, new Color(0.10f, 0.12f, 0.16f, 0.98f));
            SetRect(workspace.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-392f, -160f), new Vector2(376f, -108f));

            var workspaceTitle = CreateText("Workspace Title", workspace.transform, "Board Shape Workspace", 24, TextAnchor.UpperLeft, new Color(0.86f, 0.91f, 0.97f, 1f));
            SetRect(workspaceTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-36f, 40f), new Vector2(18f, -18f));

            var toolRow = CreateHorizontalPanelContent("Grid Tools", workspace.transform, 10f, 48f);
            SetRect(toolRow.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(520f, 48f), new Vector2(18f, -58f));
            var undoButton = CreateActionButton(toolRow.transform, "Undo Button", "Undo");
            var redoButton = CreateActionButton(toolRow.transform, "Redo Button", "Redo");
            var clearButton = CreateActionButton(toolRow.transform, "Clear Button", "Clear");
            var zoomOutButton = CreateActionButton(toolRow.transform, "Zoom Out Button", "Zoom -");
            var zoomInButton = CreateActionButton(toolRow.transform, "Zoom In Button", "Zoom +");
            StretchButton(undoButton);
            StretchButton(redoButton);
            StretchButton(clearButton);
            StretchButton(zoomOutButton);
            StretchButton(zoomInButton);

            var gridScroll = CreateScrollGridArea("Board Grid Area", workspace.transform, out var gridContent, 1180f, 740f);
            SetRect(gridScroll.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-36f, -144f), new Vector2(18f, 18f));
            var boardGrid = gridScroll.gameObject.AddComponent<ShapeGridEditor>();
            ConfigureGridEditor(boardGrid, gridContent, 10, 8);

            var controllerObject = new GameObject("Board Editor Controller");
            controllerObject.transform.SetParent(canvas.transform, false);
            var boardController = controllerObject.AddComponent<BoardEditorController>();
            ConfigureBoardController(boardController, boardGrid, boardIdInput, columnsInput, rowsInput, statusText, statisticsText, boardListContent);
            BindBoardButtons(boardController, boardGrid, newButton, loadButton, saveButton, deleteButton, applySizeButton, undoButton, redoButton, clearButton, zoomOutButton, zoomInButton);

            EditorSceneManager.SaveScene(scene, BoardEditorScenePath);
        }

        private static void BuildCardEditorScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "CardEditorScene";

            CreateCamera("Main Camera", new Color(0.08f, 0.09f, 0.11f, 1f));
            CreateEventSystem();

            var canvas = CreateCanvas("Card Editor Canvas");
            CreateFullScreenPanel("Background", canvas.transform, new Color(0.08f, 0.09f, 0.11f, 1f));

            var header = CreateFullWidthPanel("Header", canvas.transform, new Color(0.12f, 0.15f, 0.19f, 0.98f), 94f, true);
            CreateText("Header Title", header.transform, "Card Editor", 38, TextAnchor.MiddleCenter, Color.white);
            var backButton = CreateMenuButton(header.transform, "Back Button", "Home", SceneButtonAction.ButtonAction.LoadScene, "StartScene", "Return to start scene");
            SetRect(backButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(140f, 52f), new Vector2(100f, 0f));

            var leftPanel = CreatePanel("Inspector Panel", canvas.transform, new Color(0.11f, 0.14f, 0.18f, 0.98f));
            SetRect(leftPanel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(430f, -122f), new Vector2(0f, -108f));

            CreateScrollList("Inspector Scroll", leftPanel.transform, out var inspectorContentTransform, 0f);
            var inspectorContent = inspectorContentTransform.GetComponent<VerticalLayoutGroup>();
            inspectorContent.spacing = 12f;
            inspectorContent.padding = new RectOffset(18, 18, 18, 18);
            inspectorContent.childForceExpandHeight = false;

            CreateTextBlock("Card Id Label", inspectorContent.transform, "Card ID", 20, 30f);
            var cardIdInput = CreateInputField("Card Id Input", inspectorContent.transform, "1", 44f, false);
            CreateTextBlock("Card Size Label", inspectorContent.transform, "Grid Size (1-99)", 20, 30f);
            var sizeRow = CreateHorizontalPanelContent("Size Row", inspectorContent.transform, 10f, 44f);
            var columnsInput = CreateInputField("Columns Input", sizeRow.transform, "8", 44f, false);
            var rowsInput = CreateInputField("Rows Input", sizeRow.transform, "8", 44f, false);
            var applySizeButton = CreateActionButton(sizeRow.transform, "Apply Size Button", "Apply");
            StretchButton(columnsInput.gameObject);
            StretchButton(rowsInput.gameObject);
            StretchButton(applySizeButton);
            CreateTextBlock("Name Label", inspectorContent.transform, "Name", 20, 30f);
            var nameInput = CreateInputField("Name Input", inspectorContent.transform, string.Empty, 44f, false);
            CreateTextBlock("Effect Label", inspectorContent.transform, "Effect", 20, 30f);
            var effectInput = CreateInputField("Effect Input", inspectorContent.transform, string.Empty, 68f, true);
            CreateTextBlock("Description Label", inspectorContent.transform, "Description", 20, 30f);
            var descriptionInput = CreateInputField("Description Input", inspectorContent.transform, string.Empty, 96f, true);
            CreateTextBlock("Notes Label", inspectorContent.transform, "Notes", 20, 30f);
            var notesInput = CreateInputField("Notes Input", inspectorContent.transform, string.Empty, 96f, true);

            var actionRow = CreateHorizontalPanelContent("Action Row", inspectorContent.transform, 10f, 52f);
            var newButton = CreateActionButton(actionRow.transform, "New Card Button", "New");
            var loadButton = CreateActionButton(actionRow.transform, "Load Card Button", "Load");
            var saveButton = CreateActionButton(actionRow.transform, "Save Card Button", "Save");
            var deleteButton = CreateActionButton(actionRow.transform, "Delete Card Button", "Delete");
            StretchButton(newButton);
            StretchButton(loadButton);
            StretchButton(saveButton);
            StretchButton(deleteButton);

            var modeRow = CreateHorizontalPanelContent("Mode Row", inspectorContent.transform, 10f, 52f);
            var editModeButton = CreateActionButton(modeRow.transform, "Edit Mode Button", "Edit Mode");
            var previewModeButton = CreateActionButton(modeRow.transform, "Preview Mode Button", "Preview Mode");
            StretchButton(editModeButton);
            StretchButton(previewModeButton);

            var modeText = CreateTextBlock("Mode Text", inspectorContent.transform, "Edit Mode", 18, 34f);
            modeText.alignment = TextAnchor.MiddleLeft;
            var statusText = CreateTextBlock("Status Text", inspectorContent.transform, "Ready.", 18, 50f);
            statusText.alignment = TextAnchor.MiddleLeft;
            var statisticsText = CreateTextBlock("Statistics Text", inspectorContent.transform, "8 x 8 | 0 cells", 16, 36f);
            statisticsText.alignment = TextAnchor.MiddleLeft;

            var rightPanel = CreatePanel("Workspace Panel", canvas.transform, new Color(0.10f, 0.12f, 0.16f, 0.98f));
            SetRect(rightPanel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-462f, -122f), new Vector2(446f, -108f));

            var previewPanel = CreatePanel("Preview Info Panel", rightPanel.transform, new Color(0.14f, 0.18f, 0.23f, 0.98f));
            SetRect(previewPanel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(340f, 320f), new Vector2(-18f, -18f));
            var previewLayout = CreateVerticalPanelContent("Preview Info Content", previewPanel.transform, 8f, new RectOffset(16, 16, 16, 16));
            previewLayout.childForceExpandHeight = false;
            var previewTitleText = CreateTextBlock("Preview Title", previewLayout.transform, "Hover a card", 24, 40f);
            var previewShapeText = CreateTextBlock("Preview Shape", previewLayout.transform, "Shape:", 16, 32f);
            var previewEffectText = CreateTextBlock("Preview Effect", previewLayout.transform, "Effect:", 18, 52f);
            previewEffectText.alignment = TextAnchor.UpperLeft;
            var previewDescriptionText = CreateTextBlock("Preview Description", previewLayout.transform, "Description:", 18, 74f);
            previewDescriptionText.alignment = TextAnchor.UpperLeft;
            var previewNotesText = CreateTextBlock("Preview Notes", previewLayout.transform, "Notes:", 18, 74f);
            previewNotesText.alignment = TextAnchor.UpperLeft;

            var gridToolbar = CreateHorizontalPanelContent("Grid Tools", rightPanel.transform, 10f, 48f);
            SetRect(gridToolbar.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(520f, 48f), new Vector2(18f, -18f));
            var undoButton = CreateActionButton(gridToolbar.transform, "Undo Button", "Undo");
            var redoButton = CreateActionButton(gridToolbar.transform, "Redo Button", "Redo");
            var clearButton = CreateActionButton(gridToolbar.transform, "Clear Button", "Clear");
            var zoomOutButton = CreateActionButton(gridToolbar.transform, "Zoom Out Button", "Zoom -");
            var zoomInButton = CreateActionButton(gridToolbar.transform, "Zoom In Button", "Zoom +");
            StretchButton(undoButton);
            StretchButton(redoButton);
            StretchButton(clearButton);
            StretchButton(zoomOutButton);
            StretchButton(zoomInButton);

            var gridScroll = CreateScrollGridArea("Card Grid Area", rightPanel.transform, out var gridContent, 920f, 600f);
            SetRect(gridScroll.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1050f, 570f), new Vector2(18f, -78f));
            var cardGrid = gridScroll.gameObject.AddComponent<ShapeGridEditor>();
            ConfigureGridEditor(cardGrid, gridContent, 8, 8);
            var hoverTarget = gridScroll.gameObject.AddComponent<CardPreviewHoverTarget>();

            var libraryPanel = CreatePanel("Card Library Panel", rightPanel.transform, new Color(0.14f, 0.18f, 0.23f, 0.98f));
            SetRect(libraryPanel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(-36f, 250f), new Vector2(18f, 18f));
            var libraryTitle = CreateText("Library Title", libraryPanel.transform, "Saved Cards", 26, TextAnchor.UpperLeft, Color.white);
            SetRect(libraryTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-32f, 36f), new Vector2(16f, -16f));
            CreateScrollList("Card Library List", libraryPanel.transform, out var cardListContent, 170f);
            SetRect(cardListContent.parent.parent.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-32f, -62f), new Vector2(16f, 16f));

            var controllerObject = new GameObject("Card Editor Controller");
            controllerObject.transform.SetParent(canvas.transform, false);
            var controller = controllerObject.AddComponent<CardEditorController>();
            ConfigureCardController(controller, cardGrid, cardIdInput, columnsInput, rowsInput, nameInput, effectInput, descriptionInput, notesInput, modeText, statusText, statisticsText, previewTitleText, previewShapeText, previewEffectText, previewDescriptionText, previewNotesText, previewPanel.gameObject, cardListContent);

            var hoverSerialized = new SerializedObject(hoverTarget);
            hoverSerialized.FindProperty("controller").objectReferenceValue = controller;
            hoverSerialized.ApplyModifiedPropertiesWithoutUndo();

            BindCardButtons(controller, cardGrid, newButton, loadButton, saveButton, deleteButton, applySizeButton, editModeButton, previewModeButton, undoButton, redoButton, clearButton, zoomOutButton, zoomInButton);
            previewPanel.gameObject.SetActive(false);

            EditorSceneManager.SaveScene(scene, CardEditorScenePath);
        }

        private static Canvas CreateCanvas(string name)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var rectTransform = canvasObject.GetComponent<RectTransform>();
            rectTransform.localScale = Vector3.one;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;

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

        private static Image CreatePanel(string name, Transform parent, Color color)
        {
            var panel = CreateUiObject(name, parent);
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

        private static Text CreateTextBlock(string name, Transform parent, string value, int size, float preferredHeight)
        {
            var text = CreateText(name, parent, value, size, TextAnchor.MiddleLeft, Color.white);
            var layout = text.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            return text;
        }

        private static VerticalLayoutGroup CreateVerticalPanelContent(string name, Transform parent, float spacing, RectOffset padding)
        {
            var content = CreateUiObject(name, parent);
            var rect = content.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            return layout;
        }

        private static HorizontalLayoutGroup CreateHorizontalPanelContent(string name, Transform parent, float spacing, float preferredHeight)
        {
            var content = CreateUiObject(name, parent);
            var layoutElement = content.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;

            var layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return layout;
        }

        private static InputField CreateInputField(string name, Transform parent, string initialValue, float preferredHeight, bool multiLine)
        {
            var root = CreateUiObject(name, parent);
            var layoutElement = root.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;

            var image = root.AddComponent<Image>();
            image.color = new Color(0.16f, 0.21f, 0.27f, 1f);

            var inputField = root.AddComponent<InputField>();
            inputField.lineType = multiLine ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;

            var text = CreateText("Text", root.transform, initialValue, 18, multiLine ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, new Color(0.95f, 0.97f, 1f, 1f));
            SetRect(text.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-24f, -16f), Vector2.zero);
            text.supportRichText = false;

            var placeholder = CreateText("Placeholder", root.transform, "Input", 18, multiLine ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, new Color(0.68f, 0.73f, 0.78f, 0.65f));
            SetRect(placeholder.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-24f, -16f), Vector2.zero);
            placeholder.supportRichText = false;

            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.text = initialValue;
            return inputField;
        }

        private static ScrollRect CreateScrollList(string name, Transform parent, out Transform content, float preferredHeight)
        {
            var root = CreateUiObject(name, parent);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var layout = root.AddComponent<LayoutElement>();
            layout.flexibleHeight = 1f;
            if (preferredHeight > 0f)
            {
                layout.preferredHeight = preferredHeight;
            }

            root.AddComponent<Image>().color = new Color(0.15f, 0.19f, 0.24f, 1f);

            var viewport = CreateUiObject("Viewport", root.transform);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -8f);
            viewport.AddComponent<Image>().color = new Color(0.13f, 0.17f, 0.22f, 1f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var contentObject = CreateUiObject("Content", viewport.transform);
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 8f;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = root.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            content = contentObject.transform;
            return scrollRect;
        }

        private static ScrollRect CreateScrollGridArea(string name, Transform parent, out RectTransform content, float width, float height)
        {
            var root = CreateUiObject(name, parent);
            SetRect(root.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(width, height), Vector2.zero);
            root.AddComponent<Image>().color = new Color(0.13f, 0.17f, 0.22f, 1f);

            var viewport = CreateUiObject("Viewport", root.transform);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(12f, 12f);
            viewportRect.offsetMax = new Vector2(-12f, -12f);
            viewport.AddComponent<Image>().color = new Color(0.10f, 0.13f, 0.17f, 1f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var contentObject = CreateUiObject("Content", viewport.transform);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            contentObject.AddComponent<GridLayoutGroup>();

            var scrollRect = root.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = content;
            scrollRect.horizontal = true;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            return scrollRect;
        }

        private static GameObject CreateMenuButton(Transform parent, string name, string label, SceneButtonAction.ButtonAction action, string sceneName, string placeholderMessage)
        {
            var buttonObject = CreateUiObject(name, parent);
            SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 72f), Vector2.zero);
            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 72f;
            layout.flexibleHeight = 1f;

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

        private static GameObject CreateActionButton(Transform parent, string name, string label)
        {
            var buttonObject = CreateUiObject(name, parent);
            SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(160f, 48f), Vector2.zero);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.46f, 0.78f, 1f);

            var button = buttonObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.30f, 0.56f, 0.88f, 1f);
            colors.pressedColor = new Color(0.14f, 0.32f, 0.58f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var labelText = CreateText("Label", buttonObject.transform, label, 20, TextAnchor.MiddleCenter, Color.white);
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

        private static void StretchButton(GameObject target)
        {
            var layout = target.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = target.AddComponent<LayoutElement>();
            }

            layout.flexibleWidth = 1f;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void ConfigureGridEditor(ShapeGridEditor editor, RectTransform content, int columns, int rows)
        {
            var gridLayout = content.GetComponent<GridLayoutGroup>();
            var cellPrefab = AssetDatabase.LoadAssetAtPath<ShapeGridCell>(EditorGridCellPrefabPath);

            var serializedObject = new SerializedObject(editor);
            serializedObject.FindProperty("gridLayout").objectReferenceValue = gridLayout;
            serializedObject.FindProperty("contentRoot").objectReferenceValue = content;
            serializedObject.FindProperty("cellPrefab").objectReferenceValue = cellPrefab;
            serializedObject.FindProperty("columns").intValue = columns;
            serializedObject.FindProperty("rows").intValue = rows;
            serializedObject.FindProperty("cellSize").floatValue = 32f;
            serializedObject.FindProperty("spacing").floatValue = 2f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBoardController(BoardEditorController controller, ShapeGridEditor gridEditor, InputField boardIdInput, InputField columnsInput, InputField rowsInput, Text statusText, Text statisticsText, Transform listRoot)
        {
            var listItemPrefab = AssetDatabase.LoadAssetAtPath<ShapeRecordListItem>(ListItemPrefabPath);
            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("gridEditor").objectReferenceValue = gridEditor;
            serializedObject.FindProperty("boardIdInput").objectReferenceValue = boardIdInput;
            serializedObject.FindProperty("columnsInput").objectReferenceValue = columnsInput;
            serializedObject.FindProperty("rowsInput").objectReferenceValue = rowsInput;
            serializedObject.FindProperty("statusText").objectReferenceValue = statusText;
            serializedObject.FindProperty("statisticsText").objectReferenceValue = statisticsText;
            serializedObject.FindProperty("listRoot").objectReferenceValue = listRoot;
            serializedObject.FindProperty("listItemPrefab").objectReferenceValue = listItemPrefab;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCardController(CardEditorController controller, ShapeGridEditor gridEditor, InputField cardIdInput, InputField columnsInput, InputField rowsInput, InputField nameInput, InputField effectInput, InputField descriptionInput, InputField notesInput, Text modeText, Text statusText, Text statisticsText, Text previewTitleText, Text previewShapeText, Text previewEffectText, Text previewDescriptionText, Text previewNotesText, GameObject previewPanel, Transform listRoot)
        {
            var listItemPrefab = AssetDatabase.LoadAssetAtPath<ShapeRecordListItem>(ListItemPrefabPath);
            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("gridEditor").objectReferenceValue = gridEditor;
            serializedObject.FindProperty("cardIdInput").objectReferenceValue = cardIdInput;
            serializedObject.FindProperty("columnsInput").objectReferenceValue = columnsInput;
            serializedObject.FindProperty("rowsInput").objectReferenceValue = rowsInput;
            serializedObject.FindProperty("nameInput").objectReferenceValue = nameInput;
            serializedObject.FindProperty("effectInput").objectReferenceValue = effectInput;
            serializedObject.FindProperty("descriptionInput").objectReferenceValue = descriptionInput;
            serializedObject.FindProperty("notesInput").objectReferenceValue = notesInput;
            serializedObject.FindProperty("modeText").objectReferenceValue = modeText;
            serializedObject.FindProperty("statusText").objectReferenceValue = statusText;
            serializedObject.FindProperty("statisticsText").objectReferenceValue = statisticsText;
            serializedObject.FindProperty("previewTitleText").objectReferenceValue = previewTitleText;
            serializedObject.FindProperty("previewShapeText").objectReferenceValue = previewShapeText;
            serializedObject.FindProperty("previewEffectText").objectReferenceValue = previewEffectText;
            serializedObject.FindProperty("previewDescriptionText").objectReferenceValue = previewDescriptionText;
            serializedObject.FindProperty("previewNotesText").objectReferenceValue = previewNotesText;
            serializedObject.FindProperty("previewPanel").objectReferenceValue = previewPanel;
            serializedObject.FindProperty("listRoot").objectReferenceValue = listRoot;
            serializedObject.FindProperty("listItemPrefab").objectReferenceValue = listItemPrefab;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindBoardButtons(BoardEditorController controller, ShapeGridEditor gridEditor, GameObject newButton, GameObject loadButton, GameObject saveButton, GameObject deleteButton, GameObject applySizeButton, GameObject undoButton, GameObject redoButton, GameObject clearButton, GameObject zoomOutButton, GameObject zoomInButton)
        {
            var newBoardButton = newButton.GetComponent<Button>();
            newBoardButton.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(newBoardButton.onClick, controller.NewBoard);

            var loadBoardButton = loadButton.GetComponent<Button>();
            loadBoardButton.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(loadBoardButton.onClick, controller.TryLoadBoardFromInput);

            var saveBoardButton = saveButton.GetComponent<Button>();
            saveBoardButton.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(saveBoardButton.onClick, controller.SaveCurrentBoard);

            var deleteBoardButton = deleteButton.GetComponent<Button>();
            deleteBoardButton.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(deleteBoardButton.onClick, controller.DeleteCurrentBoard);

            UnityEventTools.AddPersistentListener(applySizeButton.GetComponent<Button>().onClick, controller.ApplyGridSize);
            UnityEventTools.AddPersistentListener(undoButton.GetComponent<Button>().onClick, gridEditor.Undo);
            UnityEventTools.AddPersistentListener(redoButton.GetComponent<Button>().onClick, gridEditor.Redo);
            UnityEventTools.AddPersistentListener(clearButton.GetComponent<Button>().onClick, gridEditor.ClearGrid);
            UnityEventTools.AddPersistentListener(zoomOutButton.GetComponent<Button>().onClick, gridEditor.ZoomOut);
            UnityEventTools.AddPersistentListener(zoomInButton.GetComponent<Button>().onClick, gridEditor.ZoomIn);
        }

        private static void BindCardButtons(CardEditorController controller, ShapeGridEditor gridEditor, GameObject newButton, GameObject loadButton, GameObject saveButton, GameObject deleteButton, GameObject applySizeButton, GameObject editModeButton, GameObject previewModeButton, GameObject undoButton, GameObject redoButton, GameObject clearButton, GameObject zoomOutButton, GameObject zoomInButton)
        {
            var newCardButton = newButton.GetComponent<Button>();
            newCardButton.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(newCardButton.onClick, controller.NewCard);

            var loadCardButton = loadButton.GetComponent<Button>();
            loadCardButton.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(loadCardButton.onClick, controller.TryLoadCardFromInput);

            var saveCardButton = saveButton.GetComponent<Button>();
            saveCardButton.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(saveCardButton.onClick, controller.SaveCurrentCard);

            var deleteCardButton = deleteButton.GetComponent<Button>();
            deleteCardButton.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(deleteCardButton.onClick, controller.DeleteCurrentCard);

            var editButton = editModeButton.GetComponent<Button>();
            editButton.onClick.RemoveAllListeners();
            UnityEventTools.AddBoolPersistentListener(editButton.onClick, controller.SetPreviewMode, false);

            var previewButton = previewModeButton.GetComponent<Button>();
            previewButton.onClick.RemoveAllListeners();
            UnityEventTools.AddBoolPersistentListener(previewButton.onClick, controller.SetPreviewMode, true);

            UnityEventTools.AddPersistentListener(applySizeButton.GetComponent<Button>().onClick, controller.ApplyGridSize);
            UnityEventTools.AddPersistentListener(undoButton.GetComponent<Button>().onClick, gridEditor.Undo);
            UnityEventTools.AddPersistentListener(redoButton.GetComponent<Button>().onClick, gridEditor.Redo);
            UnityEventTools.AddPersistentListener(clearButton.GetComponent<Button>().onClick, gridEditor.ClearGrid);
            UnityEventTools.AddPersistentListener(zoomOutButton.GetComponent<Button>().onClick, gridEditor.ZoomOut);
            UnityEventTools.AddPersistentListener(zoomInButton.GetComponent<Button>().onClick, gridEditor.ZoomIn);
        }

        private static void ValidateEditorScene<TController>(string scenePath, params string[] serializedReferenceNames) where TController : Component
        {
            if (!File.Exists(scenePath))
            {
                throw new BuildFailedException($"Scene does not exist: {scenePath}");
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var controller = Object.FindObjectOfType<TController>(true);
            if (controller == null)
            {
                throw new BuildFailedException($"{scenePath} is missing {typeof(TController).Name}.");
            }

            var serializedController = new SerializedObject(controller);
            for (var i = 0; i < serializedReferenceNames.Length; i++)
            {
                var propertyName = serializedReferenceNames[i];
                var property = serializedController.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new BuildFailedException($"{typeof(TController).Name}.{propertyName} is not assigned in {scenePath}.");
                }
            }

            var gridEditor = Object.FindObjectOfType<ShapeGridEditor>(true);
            if (gridEditor == null)
            {
                throw new BuildFailedException($"{scenePath} is missing ShapeGridEditor.");
            }

            var serializedGrid = new SerializedObject(gridEditor);
            var gridReferences = new[] { "gridLayout", "contentRoot", "cellPrefab" };
            for (var i = 0; i < gridReferences.Length; i++)
            {
                var property = serializedGrid.FindProperty(gridReferences[i]);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new BuildFailedException($"ShapeGridEditor.{gridReferences[i]} is not assigned in {scenePath}.");
                }
            }

            var buttons = Object.FindObjectsOfType<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].gameObject.scene == scene && buttons[i].onClick.GetPersistentEventCount() == 0)
                {
                    throw new BuildFailedException($"Button has no persistent action in {scenePath}: {buttons[i].name}");
                }
            }
        }

        private static void ValidateJsonRoundTrip()
        {
            var path = Path.Combine(Application.temporaryCachePath, "card-game-json-validation.json");
            var database = new BoardShapeDatabase();
            database.boards.Add(new BoardShapeData
            {
                id = 42,
                columns = 3,
                rows = 2,
                cells = new System.Collections.Generic.List<GridPoint> { new GridPoint(1, 1) }
            });

            if (!ShapeEditorFileStore.TryWriteJson(path, database, out var writeError))
            {
                throw new BuildFailedException(writeError);
            }

            if (!ShapeEditorFileStore.TryReadJson(path, out BoardShapeDatabase restored, out var readError) ||
                restored == null || restored.boards == null || restored.boards.Count != 1 || restored.boards[0].cells.Count != 1)
            {
                throw new BuildFailedException(string.IsNullOrEmpty(readError) ? "JSON round-trip returned unexpected data." : readError);
            }

            File.Delete(path);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(StartScenePath, true),
                new EditorBuildSettingsScene(BattleScenePath, true),
                new EditorBuildSettingsScene(BoardEditorScenePath, true),
                new EditorBuildSettingsScene(CardEditorScenePath, true)
            };
        }
    }
}
