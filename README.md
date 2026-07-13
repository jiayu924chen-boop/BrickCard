# Card Game

Unity 2022.3.62f2 foundation project for a 16:9 card battle game.

## Scenes

- `Assets/Scenes/StartScene.unity`: start page with buttons for start game, level selection, and settings.
- `Assets/Scenes/BattleScene.unity`: battle page with a lower-middle 10 x 8 board.

## Foundation Notes

- The UI Canvas uses a 1920 x 1080 reference resolution and scales to Windows/macOS desktop windows.
- The 10 x 8 board is built with a `GridLayoutGroup`; each cell is an instance of `Assets/Prefabs/BoardCell.prefab`.
- The only runtime script is `SceneButtonAction`, used by button `OnClick` events for scene navigation and placeholders.
- To regenerate the foundation scenes, use Unity menu `Card Game > Build Foundation Scenes`.
