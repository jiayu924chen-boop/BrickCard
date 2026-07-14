# Card Game

Unity 2022.3.62f2 foundation project for a 16:9 card battle game targeting Windows and macOS.

## Scenes

- `Assets/Scenes/StartScene.unity`: start page and navigation to battle/content editors.
- `Assets/Scenes/BattleScene.unity`: battle foundation with a lower-middle 10 x 8 board.
- `Assets/Scenes/BoardEditorScene.unity`: board shape editor and board library.
- `Assets/Scenes/CardEditorScene.unity`: card shape/metadata editor, card library, and hover preview.

## Shape Editors

- Grid width and height are independently configurable from 1 to 99.
- Hold the left mouse button and drag across cells to paint; right-click a cell to erase it.
- Undo, redo, clear, and zoom controls are scene components with persistent `OnClick` bindings.
- `Ctrl/Cmd+Z`, `Ctrl+Y`, `Cmd+Shift+Z`, and `Ctrl/Cmd+S` are supported.
- New records choose the first available positive ID. Delete requires a second click within three seconds.
- Board and card data include dimensions and normalized, de-duplicated cell coordinates.

## Data

- In the Unity Editor, data is stored in `Assets/StreamingAssets/CardGameEditorData` so it can be versioned with the project.
- All boards are stored in `boards.json`.
- Each card is stored in `cards/card_<id>.json` and includes name, effect, description, and notes fields.
- Windows/macOS players copy packaged seed data on first use and write edits to `Application.persistentDataPath/CardGameEditorData`.
- Writes use a temporary file and replacement step to reduce the risk of partial JSON files.

## Maintenance

- Use `Card Game > Build Foundation Scenes` to regenerate scenes and reusable UI prefabs.
- Use `Card Game > Validate Foundation Scenes` to check scene references, button bindings, Build Settings, and JSON round-trip behavior.
- The UI uses a 1920 x 1080 CanvasScaler reference resolution and resizable desktop windows.
