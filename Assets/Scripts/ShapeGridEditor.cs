using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CardGame.Editors
{
    public sealed class ShapeGridEditor : MonoBehaviour
    {
        [SerializeField] private GridLayoutGroup gridLayout;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private ShapeGridCell cellPrefab;
        [SerializeField, Range(1, ShapeEditorPaths.MaxGridSize)] private int columns = 10;
        [SerializeField, Range(1, ShapeEditorPaths.MaxGridSize)] private int rows = 8;
        [SerializeField, Range(12f, 64f)] private float cellSize = 32f;
        [SerializeField] private float spacing = 2f;
        [SerializeField] private bool editable = true;
        [SerializeField] private int historyLimit = 50;

        private readonly List<ShapeGridCell> cellViews = new List<ShapeGridCell>();
        private readonly Stack<GridSnapshot> undoStack = new Stack<GridSnapshot>();
        private readonly Stack<GridSnapshot> redoStack = new Stack<GridSnapshot>();
        private bool[,] filledCells;
        private bool paintValue;
        private int filledCount;

        public event Action Changed;
        public event Action ViewChanged;
        public event Action<int, int, bool> HoverChanged;

        public bool Editable => editable;
        public bool IsPainting { get; private set; }
        public int Columns => columns;
        public int Rows => rows;
        public int FilledCount => filledCount;
        public float CellSize => cellSize;
        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;

        private void Awake()
        {
            RebuildGrid(null);
        }

        private void Update()
        {
            if (IsPainting && !Input.GetMouseButton(0))
            {
                EndPaint();
            }

            if (!editable || !IsPrimaryShortcutHeld())
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Z))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    Redo();
                }
                else
                {
                    Undo();
                }
            }
            else if (Input.GetKeyDown(KeyCode.Y))
            {
                Redo();
            }
        }

        public void SetEditable(bool value)
        {
            editable = value;
            if (!editable)
            {
                EndPaint();
            }
        }

        public void SetDimensions(int newColumns, int newRows, bool preserveCells)
        {
            newColumns = Mathf.Clamp(newColumns, 1, ShapeEditorPaths.MaxGridSize);
            newRows = Mathf.Clamp(newRows, 1, ShapeEditorPaths.MaxGridSize);
            if (newColumns == columns && newRows == rows)
            {
                return;
            }

            PushUndoSnapshot();
            var retainedCells = preserveCells ? GetFilledCells() : null;
            columns = newColumns;
            rows = newRows;
            RebuildGrid(retainedCells);
            Changed?.Invoke();
        }

        public void LoadShape(int newColumns, int newRows, IReadOnlyList<GridPoint> cells)
        {
            columns = Mathf.Clamp(newColumns, 1, ShapeEditorPaths.MaxGridSize);
            rows = Mathf.Clamp(newRows, 1, ShapeEditorPaths.MaxGridSize);
            RebuildGrid(cells);
            ClearHistory();
            Changed?.Invoke();
        }

        public void ResetGrid(int newColumns, int newRows)
        {
            LoadShape(newColumns, newRows, null);
        }

        public void BeginPaint(bool value)
        {
            if (!editable || IsPainting)
            {
                return;
            }

            PushUndoSnapshot();
            IsPainting = true;
            paintValue = value;
        }

        public void EndPaint()
        {
            IsPainting = false;
        }

        public void ApplyPaint(int x, int y)
        {
            SetCell(x, y, paintValue);
        }

        public void EraseSingleCell(int x, int y)
        {
            if (!editable || !IsValidCell(x, y) || !filledCells[x, y])
            {
                return;
            }

            PushUndoSnapshot();
            SetCell(x, y, false);
        }

        public void ClearGrid()
        {
            EnsureGridBuilt();
            if (filledCount == 0)
            {
                return;
            }

            PushUndoSnapshot();
            ApplySnapshot(new GridSnapshot(columns, rows, new List<GridPoint>()), false);
            Changed?.Invoke();
        }

        public void Undo()
        {
            if (undoStack.Count == 0)
            {
                return;
            }

            redoStack.Push(CaptureSnapshot());
            ApplySnapshot(undoStack.Pop(), true);
        }

        public void Redo()
        {
            if (redoStack.Count == 0)
            {
                return;
            }

            undoStack.Push(CaptureSnapshot());
            ApplySnapshot(redoStack.Pop(), true);
        }

        public void ZoomIn()
        {
            SetCellSize(cellSize + 4f);
        }

        public void ZoomOut()
        {
            SetCellSize(cellSize - 4f);
        }

        public void SetCellSize(float value)
        {
            cellSize = Mathf.Clamp(value, 12f, 64f);
            ApplyLayoutSettings();
            ViewChanged?.Invoke();
        }

        public List<GridPoint> GetFilledCells()
        {
            EnsureGridBuilt();
            var result = new List<GridPoint>(filledCount);
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    if (filledCells[x, y])
                    {
                        result.Add(new GridPoint(x, y));
                    }
                }
            }

            return result;
        }

        public void SetHoveredCell(int x, int y, bool isHovered)
        {
            HoverChanged?.Invoke(x, y, isHovered);
        }

        private void SetCell(int x, int y, bool value)
        {
            EnsureGridBuilt();
            if (!IsValidCell(x, y) || filledCells[x, y] == value)
            {
                return;
            }

            filledCells[x, y] = value;
            filledCount += value ? 1 : -1;
            cellViews[GetIndex(x, y)].SetFilled(value);
            Changed?.Invoke();
        }

        private void RebuildGrid(IReadOnlyList<GridPoint> cells)
        {
            if (gridLayout == null)
            {
                gridLayout = GetComponentInChildren<GridLayoutGroup>(true);
            }

            if (contentRoot == null && gridLayout != null)
            {
                contentRoot = gridLayout.GetComponent<RectTransform>();
            }

            if (gridLayout == null || contentRoot == null || cellPrefab == null)
            {
                Debug.LogError("ShapeGridEditor is missing a grid layout, content root, or cell prefab.", this);
                enabled = false;
                return;
            }

            columns = Mathf.Clamp(columns, 1, ShapeEditorPaths.MaxGridSize);
            rows = Mathf.Clamp(rows, 1, ShapeEditorPaths.MaxGridSize);
            filledCells = new bool[columns, rows];
            filledCount = 0;

            var requiredViewCount = columns * rows;
            gridLayout.enabled = false;
            while (cellViews.Count < requiredViewCount)
            {
                cellViews.Add(Instantiate(cellPrefab, contentRoot));
            }

            for (var index = 0; index < cellViews.Count; index++)
            {
                var active = index < requiredViewCount;
                cellViews[index].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                var x = index % columns;
                var y = index / columns;
                cellViews[index].name = $"Cell_{x:00}_{y:00}";
                cellViews[index].Initialize(this, x, y);
            }

            if (cells != null)
            {
                for (var i = 0; i < cells.Count; i++)
                {
                    var point = cells[i];
                    if (!IsValidCell(point.x, point.y) || filledCells[point.x, point.y])
                    {
                        continue;
                    }

                    filledCells[point.x, point.y] = true;
                    filledCount++;
                    cellViews[GetIndex(point.x, point.y)].SetFilled(true);
                }
            }

            gridLayout.enabled = true;
            ApplyLayoutSettings();
        }

        private void ApplyLayoutSettings()
        {
            if (gridLayout == null || contentRoot == null)
            {
                return;
            }

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columns;
            gridLayout.cellSize = new Vector2(cellSize, cellSize);
            gridLayout.spacing = new Vector2(spacing, spacing);
            contentRoot.sizeDelta = new Vector2(
                columns * cellSize + (columns - 1) * spacing,
                rows * cellSize + (rows - 1) * spacing);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }

        private void EnsureGridBuilt()
        {
            if (filledCells == null)
            {
                RebuildGrid(null);
            }
        }

        private bool IsValidCell(int x, int y)
        {
            return x >= 0 && x < columns && y >= 0 && y < rows;
        }

        private int GetIndex(int x, int y)
        {
            return y * columns + x;
        }

        private static bool IsPrimaryShortcutHeld()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                   Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        }

        private GridSnapshot CaptureSnapshot()
        {
            return new GridSnapshot(columns, rows, GetFilledCells());
        }

        private void PushUndoSnapshot()
        {
            undoStack.Push(CaptureSnapshot());
            redoStack.Clear();
            TrimHistory(undoStack);
        }

        private void ApplySnapshot(GridSnapshot snapshot, bool notify)
        {
            columns = snapshot.Columns;
            rows = snapshot.Rows;
            RebuildGrid(snapshot.Cells);
            if (notify)
            {
                Changed?.Invoke();
            }
        }

        private void ClearHistory()
        {
            undoStack.Clear();
            redoStack.Clear();
        }

        private void TrimHistory(Stack<GridSnapshot> history)
        {
            if (history.Count <= historyLimit)
            {
                return;
            }

            var snapshots = history.ToArray();
            history.Clear();
            for (var i = Mathf.Min(historyLimit, snapshots.Length) - 1; i >= 0; i--)
            {
                history.Push(snapshots[i]);
            }
        }

        private sealed class GridSnapshot
        {
            public GridSnapshot(int columns, int rows, List<GridPoint> cells)
            {
                Columns = columns;
                Rows = rows;
                Cells = cells;
            }

            public int Columns { get; }
            public int Rows { get; }
            public List<GridPoint> Cells { get; }
        }
    }
}
