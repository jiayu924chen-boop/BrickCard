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
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private ShapeGridCell cellPrefab;
        [SerializeField, Range(1, ShapeEditorPaths.MaxGridSize)] private int columns = 10;
        [SerializeField, Range(1, ShapeEditorPaths.MaxGridSize)] private int rows = 8;
        [SerializeField, Range(12f, 64f)] private float cellSize = 32f;
        [SerializeField] private float spacing = 2f;
        [SerializeField, Range(0f, 32f)] private float viewportPadding = 8f;
        [SerializeField] private bool editable = true;
        [SerializeField] private int historyLimit = 50;

        private readonly List<ShapeGridCell> cellViews = new List<ShapeGridCell>();
        private readonly Stack<GridSnapshot> undoStack = new Stack<GridSnapshot>();
        private readonly Stack<GridSnapshot> redoStack = new Stack<GridSnapshot>();
        private bool[,] filledCells;
        private bool paintValue;
        private int filledCount;
        private bool referencesCached;
        private int hoveredX = -1;
        private int hoveredY = -1;
        private bool restoreScrollRectEnabled;

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

        private void OnRectTransformDimensionsChange()
        {
            if (!referencesCached || !isActiveAndEnabled)
            {
                return;
            }

            ApplyLayoutSettings();
        }

        private void Update()
        {
            HandlePointerInput();

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
                ClearHover();
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
            if (scrollRect != null)
            {
                restoreScrollRectEnabled = scrollRect.enabled;
                scrollRect.enabled = false;
            }
        }

        public void EndPaint()
        {
            if (scrollRect != null)
            {
                scrollRect.enabled = restoreScrollRectEnabled;
            }

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

        private void HandlePointerInput()
        {
            CacheReferences();
            if (!referencesCached)
            {
                return;
            }

            if (!editable)
            {
                return;
            }

            var screenPoint = Input.mousePosition;
            var hasHoveredCell = TryGetCellAtScreenPoint(screenPoint, out var x, out var y);
            UpdateHover(hasHoveredCell, x, y);

            if (Input.GetMouseButtonDown(0) && hasHoveredCell)
            {
                BeginPaint(true);
                ApplyPaint(x, y);
            }

            if (IsPainting)
            {
                if (Input.GetMouseButton(0))
                {
                    if (hasHoveredCell)
                    {
                        ApplyPaint(x, y);
                    }
                }
                else
                {
                    EndPaint();
                }
            }

            if (Input.GetMouseButtonDown(1) && hasHoveredCell)
            {
                EraseSingleCell(x, y);
            }
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
            CacheReferences();
            if (!referencesCached)
            {
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
                cellViews[index].Initialize();
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
            CacheReferences();
            if (!referencesCached)
            {
                return;
            }

            var gridWidth = columns * cellSize + Mathf.Max(0, columns - 1) * spacing;
            var gridHeight = rows * cellSize + Mathf.Max(0, rows - 1) * spacing;
            var viewportWidth = viewportRect != null ? viewportRect.rect.width : 0f;
            var viewportHeight = viewportRect != null ? viewportRect.rect.height : 0f;

            var extraHorizontal = Mathf.Max(0f, viewportWidth - gridWidth - (viewportPadding * 2f));
            var extraVertical = Mathf.Max(0f, viewportHeight - gridHeight - (viewportPadding * 2f));
            var leftPadding = Mathf.RoundToInt(viewportPadding + (extraHorizontal * 0.5f));
            var rightPadding = Mathf.RoundToInt(viewportPadding + (extraHorizontal * 0.5f));
            var topPadding = Mathf.RoundToInt(viewportPadding + (extraVertical * 0.5f));
            var bottomPadding = Mathf.RoundToInt(viewportPadding + (extraVertical * 0.5f));

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columns;
            gridLayout.cellSize = new Vector2(cellSize, cellSize);
            gridLayout.spacing = new Vector2(spacing, spacing);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.padding = new RectOffset(leftPadding, rightPadding, topPadding, bottomPadding);

            var contentWidth = gridWidth + leftPadding + rightPadding;
            var contentHeight = gridHeight + topPadding + bottomPadding;
            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(0f, 1f);
            contentRoot.pivot = new Vector2(0f, 1f);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.sizeDelta = new Vector2(contentWidth, contentHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }

        private void EnsureGridBuilt()
        {
            if (filledCells == null)
            {
                RebuildGrid(null);
            }
        }

        private bool TryGetCellAtScreenPoint(Vector2 screenPoint, out int x, out int y)
        {
            x = -1;
            y = -1;

            if (viewportRect == null || contentRoot == null)
            {
                return false;
            }

            if (!RectTransformUtility.RectangleContainsScreenPoint(viewportRect, screenPoint))
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRoot, screenPoint, null, out var localPoint))
            {
                return false;
            }

            var localX = localPoint.x - gridLayout.padding.left;
            var localY = -localPoint.y - gridLayout.padding.top;
            var gridWidth = columns * cellSize + Mathf.Max(0, columns - 1) * spacing;
            var gridHeight = rows * cellSize + Mathf.Max(0, rows - 1) * spacing;
            if (localX < 0f || localY < 0f || localX > gridWidth || localY > gridHeight)
            {
                return false;
            }

            var stride = cellSize + spacing;
            x = Mathf.Clamp(Mathf.FloorToInt(localX / stride), 0, columns - 1);
            y = Mathf.Clamp(Mathf.FloorToInt(localY / stride), 0, rows - 1);
            return IsValidCell(x, y);
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

        private void UpdateHover(bool hasHoveredCell, int x, int y)
        {
            if (hasHoveredCell && x == hoveredX && y == hoveredY)
            {
                return;
            }

            ClearHover();
            if (!hasHoveredCell)
            {
                return;
            }

            hoveredX = x;
            hoveredY = y;
            HoverChanged?.Invoke(hoveredX, hoveredY, true);
        }

        private void ClearHover()
        {
            if (hoveredX < 0 || hoveredY < 0)
            {
                return;
            }

            HoverChanged?.Invoke(hoveredX, hoveredY, false);
            hoveredX = -1;
            hoveredY = -1;
        }

        private void CacheReferences()
        {
            if (referencesCached)
            {
                return;
            }

            if (gridLayout == null)
            {
                gridLayout = GetComponentInChildren<GridLayoutGroup>(true);
            }

            if (contentRoot == null && gridLayout != null)
            {
                contentRoot = gridLayout.GetComponent<RectTransform>();
            }

            if (scrollRect == null)
            {
                scrollRect = GetComponent<ScrollRect>();
                if (scrollRect == null)
                {
                    scrollRect = GetComponentInChildren<ScrollRect>(true);
                }
            }

            if (viewportRect == null)
            {
                viewportRect = scrollRect != null && scrollRect.viewport != null
                    ? scrollRect.viewport
                    : contentRoot != null ? contentRoot.parent as RectTransform : null;
            }

            if (gridLayout == null || contentRoot == null || cellPrefab == null)
            {
                Debug.LogError("ShapeGridEditor is missing a grid layout, content root, or cell prefab.", this);
                return;
            }

            referencesCached = true;
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
