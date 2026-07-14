using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CardGame.Editors
{
    public sealed class BoardEditorController : MonoBehaviour
    {
        private const int DefaultColumns = 10;
        private const int DefaultRows = 8;

        [SerializeField] private ShapeGridEditor gridEditor;
        [SerializeField] private InputField boardIdInput;
        [SerializeField] private InputField columnsInput;
        [SerializeField] private InputField rowsInput;
        [SerializeField] private Text statusText;
        [SerializeField] private Text statisticsText;
        [SerializeField] private Transform listRoot;
        [SerializeField] private ShapeRecordListItem listItemPrefab;

        private bool dirty;
        private bool suppressDirtyTracking;
        private int pendingDeleteId = -1;
        private float pendingDeleteUntil;

        private void OnEnable()
        {
            if (gridEditor != null)
            {
                gridEditor.Changed += HandleShapeChanged;
                gridEditor.ViewChanged += HandleViewChanged;
                gridEditor.HoverChanged += HandleHoverChanged;
            }
        }

        private void OnDisable()
        {
            if (gridEditor != null)
            {
                gridEditor.Changed -= HandleShapeChanged;
                gridEditor.ViewChanged -= HandleViewChanged;
                gridEditor.HoverChanged -= HandleHoverChanged;
            }
        }

        private void Start()
        {
            ShapeEditorPaths.EnsureDirectories();
            ConfigureIntegerInput(boardIdInput);
            ConfigureIntegerInput(columnsInput);
            ConfigureIntegerInput(rowsInput);

            if (string.IsNullOrWhiteSpace(boardIdInput.text))
            {
                boardIdInput.text = "1";
            }

            boardIdInput.onValueChanged.AddListener(_ => MarkDirty());
            RefreshBoardList();
            TryLoadBoardFromInput();
        }

        private void Update()
        {
            if (IsPrimaryShortcutHeld() && Input.GetKeyDown(KeyCode.S))
            {
                SaveCurrentBoard();
            }
        }

        public void NewBoard()
        {
            if (!TryLoadDatabase(out var database))
            {
                return;
            }

            var nextId = FindNextAvailableId(database.boards.Select(item => item.id));
            if (nextId < 0)
            {
                SetStatus("No free board ID is available.");
                return;
            }

            suppressDirtyTracking = true;
            boardIdInput.text = nextId.ToString();
            gridEditor.SetEditable(true);
            gridEditor.ResetGrid(DefaultColumns, DefaultRows);
            SyncDimensionInputs();
            suppressDirtyTracking = false;
            dirty = true;
            SetStatus("New board created. Save to add it to the library.");
            UpdateStatistics();
        }

        public void TryLoadBoardFromInput()
        {
            if (!TryGetBoardId(out var boardId) || !TryLoadDatabase(out var database))
            {
                return;
            }

            var board = database.boards.FirstOrDefault(item => item.id == boardId);
            suppressDirtyTracking = true;
            if (board == null)
            {
                gridEditor.ResetGrid(DefaultColumns, DefaultRows);
                SetStatus($"Board {boardId} does not exist. Editing a new board.");
                dirty = true;
            }
            else
            {
                ShapeEditorDataUtility.Normalize(board);
                gridEditor.LoadShape(board.columns, board.rows, board.cells);
                SetStatus($"Loaded board {boardId}.");
                dirty = false;
            }

            SyncDimensionInputs();
            suppressDirtyTracking = false;
            UpdateStatistics();
        }

        public void ApplyGridSize()
        {
            if (!TryGetGridSize(out var columns, out var rows))
            {
                return;
            }

            gridEditor.SetDimensions(columns, rows, true);
            SyncDimensionInputs();
            SetStatus($"Board resized to {columns} x {rows}. Cells outside the new bounds were removed.");
        }

        public void SaveCurrentBoard()
        {
            if (!TryGetBoardId(out var boardId) || !TryLoadDatabase(out var database))
            {
                return;
            }

            var board = database.boards.FirstOrDefault(item => item.id == boardId);
            if (board == null)
            {
                board = new BoardShapeData { id = boardId };
                database.boards.Add(board);
            }

            board.columns = gridEditor.Columns;
            board.rows = gridEditor.Rows;
            board.cells = gridEditor.GetFilledCells();
            ShapeEditorDataUtility.Normalize(board);
            database.boards = database.boards.OrderBy(item => item.id).ToList();

            if (!ShapeEditorFileStore.TryWriteJson(ShapeEditorPaths.BoardDatabasePath, database, out var error))
            {
                SetStatus(error);
                return;
            }

            dirty = false;
            pendingDeleteId = -1;
            RefreshBoardList();
            SetStatus($"Saved board {boardId}.");
            UpdateStatistics();
        }

        public void DeleteCurrentBoard()
        {
            if (!TryGetBoardId(out var boardId) || !TryLoadDatabase(out var database))
            {
                return;
            }

            if (pendingDeleteId != boardId || Time.unscaledTime > pendingDeleteUntil)
            {
                pendingDeleteId = boardId;
                pendingDeleteUntil = Time.unscaledTime + 3f;
                SetStatus($"Click Delete again within 3 seconds to remove board {boardId}.");
                return;
            }

            pendingDeleteId = -1;
            var removed = database.boards.RemoveAll(item => item.id == boardId);
            if (removed == 0)
            {
                SetStatus($"Board {boardId} was not found.");
                return;
            }

            if (!ShapeEditorFileStore.TryWriteJson(ShapeEditorPaths.BoardDatabasePath, database, out var error))
            {
                SetStatus(error);
                return;
            }

            suppressDirtyTracking = true;
            gridEditor.ResetGrid(DefaultColumns, DefaultRows);
            SyncDimensionInputs();
            suppressDirtyTracking = false;
            dirty = false;
            RefreshBoardList();
            SetStatus($"Deleted board {boardId}.");
            UpdateStatistics();
        }

        public void RefreshBoardList()
        {
            ClearList();
            if (!TryLoadDatabase(out var database))
            {
                return;
            }

            foreach (var board in database.boards.OrderBy(item => item.id))
            {
                ShapeEditorDataUtility.Normalize(board);
                var capturedId = board.id;
                var item = Instantiate(listItemPrefab, listRoot);
                item.Initialize(
                    $"Board {capturedId:000}  |  {board.columns}x{board.rows}  |  {board.cells.Count} cells",
                    () =>
                    {
                        boardIdInput.SetTextWithoutNotify(capturedId.ToString());
                        TryLoadBoardFromInput();
                    });
            }
        }

        private bool TryLoadDatabase(out BoardShapeDatabase database)
        {
            ShapeEditorPaths.EnsureDirectories();
            if (!ShapeEditorFileStore.TryReadJson(ShapeEditorPaths.BoardDatabasePath, out database, out var error))
            {
                SetStatus(error);
                return false;
            }

            database = database ?? new BoardShapeDatabase();
            database.boards = database.boards ?? new List<BoardShapeData>();
            database.boards.RemoveAll(item => item == null);
            foreach (var board in database.boards)
            {
                ShapeEditorDataUtility.Normalize(board);
            }

            database.boards = database.boards
                .Where(item => item.id >= 0 && item.id <= ShapeEditorPaths.MaxRecordId)
                .GroupBy(item => item.id)
                .Select(group => group.Last())
                .OrderBy(item => item.id)
                .ToList();

            return true;
        }

        private bool TryGetBoardId(out int boardId)
        {
            if (!int.TryParse(boardIdInput.text, out boardId) || boardId < 0 || boardId > ShapeEditorPaths.MaxRecordId)
            {
                SetStatus($"Board ID must be between 0 and {ShapeEditorPaths.MaxRecordId}.");
                return false;
            }

            return true;
        }

        private bool TryGetGridSize(out int columns, out int rows)
        {
            columns = 0;
            rows = 0;
            if (!int.TryParse(columnsInput.text, out columns) || !int.TryParse(rowsInput.text, out rows) ||
                columns < 1 || columns > ShapeEditorPaths.MaxGridSize || rows < 1 || rows > ShapeEditorPaths.MaxGridSize)
            {
                SetStatus($"Width and height must be between 1 and {ShapeEditorPaths.MaxGridSize}.");
                return false;
            }

            return true;
        }

        private void HandleShapeChanged()
        {
            MarkDirty();
            UpdateStatistics();
        }

        private void HandleHoverChanged(int x, int y, bool isHovered)
        {
            UpdateStatistics(isHovered ? $" | Cursor {x + 1},{y + 1}" : string.Empty);
        }

        private void HandleViewChanged()
        {
            UpdateStatistics();
        }

        private void MarkDirty()
        {
            if (!suppressDirtyTracking)
            {
                dirty = true;
                UpdateStatistics();
            }
        }

        private void SyncDimensionInputs()
        {
            columnsInput.SetTextWithoutNotify(gridEditor.Columns.ToString());
            rowsInput.SetTextWithoutNotify(gridEditor.Rows.ToString());
        }

        private void UpdateStatistics(string suffix = "")
        {
            if (statisticsText != null)
            {
                statisticsText.text = $"{gridEditor.Columns} x {gridEditor.Rows} | {gridEditor.FilledCount} cells | Zoom {gridEditor.CellSize:0}px{(dirty ? " | Unsaved" : string.Empty)}{suffix}";
            }
        }

        private void ClearList()
        {
            foreach (Transform child in listRoot)
            {
                Destroy(child.gameObject);
            }
        }

        private static void ConfigureIntegerInput(InputField input)
        {
            input.contentType = InputField.ContentType.IntegerNumber;
        }

        private static int FindNextAvailableId(IEnumerable<int> existingIds)
        {
            var used = new HashSet<int>(existingIds);
            for (var id = 1; id <= ShapeEditorPaths.MaxRecordId; id++)
            {
                if (!used.Contains(id))
                {
                    return id;
                }
            }

            return -1;
        }

        private static bool IsPrimaryShortcutHeld()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                   Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}
