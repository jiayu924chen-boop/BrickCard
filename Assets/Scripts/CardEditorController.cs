using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CardGame.Editors
{
    public sealed class CardEditorController : MonoBehaviour
    {
        private const int DefaultColumns = 8;
        private const int DefaultRows = 8;

        [SerializeField] private ShapeGridEditor gridEditor;
        [SerializeField] private InputField cardIdInput;
        [SerializeField] private InputField columnsInput;
        [SerializeField] private InputField rowsInput;
        [SerializeField] private InputField nameInput;
        [SerializeField] private InputField effectInput;
        [SerializeField] private InputField descriptionInput;
        [SerializeField] private InputField notesInput;
        [SerializeField] private Text modeText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text statisticsText;
        [SerializeField] private Text previewTitleText;
        [SerializeField] private Text previewShapeText;
        [SerializeField] private Text previewEffectText;
        [SerializeField] private Text previewDescriptionText;
        [SerializeField] private Text previewNotesText;
        [SerializeField] private GameObject previewPanel;
        [SerializeField] private Transform listRoot;
        [SerializeField] private ShapeRecordListItem listItemPrefab;

        private readonly Dictionary<int, CardShapeData> cachedCards = new Dictionary<int, CardShapeData>();
        private bool previewMode;
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
            ConfigureIntegerInput(cardIdInput);
            ConfigureIntegerInput(columnsInput);
            ConfigureIntegerInput(rowsInput);

            if (string.IsNullOrWhiteSpace(cardIdInput.text))
            {
                cardIdInput.text = "1";
            }

            cardIdInput.onValueChanged.AddListener(_ => MarkDirty());
            nameInput.onValueChanged.AddListener(_ => HandleMetadataChanged());
            effectInput.onValueChanged.AddListener(_ => HandleMetadataChanged());
            descriptionInput.onValueChanged.AddListener(_ => HandleMetadataChanged());
            notesInput.onValueChanged.AddListener(_ => HandleMetadataChanged());

            SetPreviewMode(false);
            RefreshCardList();
            TryLoadCardFromInput();
        }

        private void Update()
        {
            if (IsPrimaryShortcutHeld() && Input.GetKeyDown(KeyCode.S))
            {
                SaveCurrentCard();
            }
        }

        public void SetPreviewMode(bool enabled)
        {
            previewMode = enabled;
            gridEditor.SetEditable(!enabled);
            nameInput.interactable = !enabled;
            effectInput.interactable = !enabled;
            descriptionInput.interactable = !enabled;
            notesInput.interactable = !enabled;
            columnsInput.interactable = !enabled;
            rowsInput.interactable = !enabled;
            modeText.text = enabled ? "Preview Mode" : "Edit Mode";

            if (enabled)
            {
                ShowCurrentCardPreview();
            }
            else
            {
                HidePreview();
            }
        }

        public void NewCard()
        {
            var cards = LoadAllCards(out _);
            var nextId = FindNextAvailableId(cards.Select(item => item.id));
            if (nextId < 0)
            {
                SetStatus("No free card ID is available.");
                return;
            }

            suppressDirtyTracking = true;
            cardIdInput.text = nextId.ToString();
            nameInput.text = string.Empty;
            effectInput.text = string.Empty;
            descriptionInput.text = string.Empty;
            notesInput.text = string.Empty;
            gridEditor.ResetGrid(DefaultColumns, DefaultRows);
            SyncDimensionInputs();
            suppressDirtyTracking = false;

            SetPreviewMode(false);
            dirty = true;
            SetStatus("New card created. Save to add it to the library.");
            UpdateStatistics();
        }

        public void TryLoadCardFromInput()
        {
            if (!TryGetCardId(out var cardId))
            {
                return;
            }

            if (!TryLoadCard(cardId, out var card, out var error))
            {
                SetStatus(error);
                return;
            }

            suppressDirtyTracking = true;
            if (card == null)
            {
                ClearEditor(DefaultColumns, DefaultRows);
                dirty = true;
                SetStatus($"Card {cardId} does not exist. Editing a new card.");
            }
            else
            {
                ApplyCardToEditor(card);
                dirty = false;
                SetStatus($"Loaded card {cardId}.");
            }

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
            SetStatus($"Card resized to {columns} x {rows}. Cells outside the new bounds were removed.");
        }

        public void SaveCurrentCard()
        {
            if (!TryGetCardId(out var cardId))
            {
                return;
            }

            var card = CreateCurrentCardData();
            card.id = cardId;
            ShapeEditorDataUtility.Normalize(card);
            if (!ShapeEditorFileStore.TryWriteJson(ShapeEditorPaths.GetCardPath(cardId), card, out var error))
            {
                SetStatus(error);
                return;
            }

            cachedCards[cardId] = CloneCard(card);
            dirty = false;
            pendingDeleteId = -1;
            RefreshCardList();
            if (previewMode)
            {
                ShowCurrentCardPreview();
            }

            SetStatus($"Saved card {cardId}.");
            UpdateStatistics();
        }

        public void DeleteCurrentCard()
        {
            if (!TryGetCardId(out var cardId))
            {
                return;
            }

            if (pendingDeleteId != cardId || Time.unscaledTime > pendingDeleteUntil)
            {
                pendingDeleteId = cardId;
                pendingDeleteUntil = Time.unscaledTime + 3f;
                SetStatus($"Click Delete again within 3 seconds to remove card {cardId}.");
                return;
            }

            pendingDeleteId = -1;
            var path = ShapeEditorPaths.GetCardPath(cardId);
            if (!File.Exists(path))
            {
                SetStatus($"Card {cardId} was not found.");
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (System.Exception exception)
            {
                SetStatus($"Could not delete card {cardId}: {exception.Message}");
                return;
            }

            cachedCards.Remove(cardId);
            suppressDirtyTracking = true;
            ClearEditor(DefaultColumns, DefaultRows);
            suppressDirtyTracking = false;
            dirty = false;
            RefreshCardList();
            SetPreviewMode(false);
            SetStatus($"Deleted card {cardId}.");
            UpdateStatistics();
        }

        public void RefreshCardList()
        {
            ClearList();
            cachedCards.Clear();
            var cards = LoadAllCards(out var skippedFiles);
            foreach (var card in cards)
            {
                cachedCards[card.id] = CloneCard(card);
                var capturedCard = CloneCard(card);
                var item = Instantiate(listItemPrefab, listRoot);
                item.Initialize(
                    $"Card {card.id:000}  |  {card.name}  |  {card.columns}x{card.rows}",
                    () =>
                    {
                        suppressDirtyTracking = true;
                        ApplyCardToEditor(capturedCard);
                        suppressDirtyTracking = false;
                        dirty = false;
                        SetStatus($"Loaded card {capturedCard.id} from the library.");
                        UpdateStatistics();
                    },
                    () => ShowPreview(capturedCard),
                    () =>
                    {
                        if (previewMode)
                        {
                            ShowCurrentCardPreview();
                        }
                        else
                        {
                            HidePreview();
                        }
                    });
            }

            if (skippedFiles > 0)
            {
                SetStatus($"Skipped {skippedFiles} invalid card file(s). Check the Console for details.");
            }
        }

        public void ShowCurrentCardPreview()
        {
            ShowPreview(CreateCurrentCardData());
        }

        public void HidePreview()
        {
            if (previewPanel != null)
            {
                previewPanel.SetActive(false);
            }
        }

        private void ShowPreview(CardShapeData card)
        {
            previewTitleText.text = string.IsNullOrWhiteSpace(card.name) ? $"Card {card.id:000}" : card.name;
            previewShapeText.text = $"Shape: {card.columns} x {card.rows} | {card.cells.Count} cells";
            previewEffectText.text = $"Effect: {card.effect}";
            previewDescriptionText.text = $"Description: {card.description}";
            previewNotesText.text = $"Notes: {card.notes}";
            previewPanel.SetActive(true);
        }

        private List<CardShapeData> LoadAllCards(out int skippedFiles)
        {
            ShapeEditorPaths.EnsureDirectories();
            skippedFiles = 0;
            var cards = new List<CardShapeData>();
            foreach (var path in Directory.GetFiles(ShapeEditorPaths.CardDatabaseDirectory, "card_*.json"))
            {
                var error = string.Empty;
                CardShapeData card = null;
                if (!TryGetCardIdFromPath(path, out var cardId) ||
                    !ShapeEditorFileStore.TryReadJson(path, out card, out error) || card == null)
                {
                    skippedFiles++;
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogWarning(error, this);
                    }
                    continue;
                }

                card.id = cardId;
                ShapeEditorDataUtility.Normalize(card);
                cards.Add(card);
            }

            return cards.OrderBy(card => card.id).ToList();
        }

        private bool TryLoadCard(int cardId, out CardShapeData card, out string error)
        {
            error = string.Empty;
            if (cachedCards.TryGetValue(cardId, out var cached))
            {
                card = CloneCard(cached);
                return true;
            }

            var path = ShapeEditorPaths.GetCardPath(cardId);
            if (!ShapeEditorFileStore.TryReadJson(path, out card, out error))
            {
                return false;
            }

            if (card != null)
            {
                card.id = cardId;
                ShapeEditorDataUtility.Normalize(card);
                cachedCards[cardId] = CloneCard(card);
            }

            return true;
        }

        private void ApplyCardToEditor(CardShapeData card)
        {
            ShapeEditorDataUtility.Normalize(card);
            cardIdInput.SetTextWithoutNotify(card.id.ToString());
            nameInput.SetTextWithoutNotify(card.name);
            effectInput.SetTextWithoutNotify(card.effect);
            descriptionInput.SetTextWithoutNotify(card.description);
            notesInput.SetTextWithoutNotify(card.notes);
            gridEditor.LoadShape(card.columns, card.rows, card.cells);
            SyncDimensionInputs();

            if (previewMode)
            {
                ShowCurrentCardPreview();
            }
            else
            {
                HidePreview();
            }
        }

        private void ClearEditor(int columns, int rows)
        {
            nameInput.SetTextWithoutNotify(string.Empty);
            effectInput.SetTextWithoutNotify(string.Empty);
            descriptionInput.SetTextWithoutNotify(string.Empty);
            notesInput.SetTextWithoutNotify(string.Empty);
            gridEditor.ResetGrid(columns, rows);
            SyncDimensionInputs();
            HidePreview();
        }

        private CardShapeData CreateCurrentCardData()
        {
            var card = new CardShapeData
            {
                columns = gridEditor.Columns,
                rows = gridEditor.Rows,
                name = nameInput.text,
                effect = effectInput.text,
                description = descriptionInput.text,
                notes = notesInput.text,
                cells = gridEditor.GetFilledCells()
            };
            int.TryParse(cardIdInput.text, out card.id);
            return card;
        }

        private static CardShapeData CloneCard(CardShapeData source)
        {
            return new CardShapeData
            {
                schemaVersion = source.schemaVersion,
                id = source.id,
                columns = source.columns,
                rows = source.rows,
                name = source.name,
                effect = source.effect,
                description = source.description,
                notes = source.notes,
                cells = new List<GridPoint>(source.cells)
            };
        }

        private bool TryGetCardId(out int cardId)
        {
            if (!int.TryParse(cardIdInput.text, out cardId) || cardId < 0 || cardId > ShapeEditorPaths.MaxRecordId)
            {
                SetStatus($"Card ID must be between 0 and {ShapeEditorPaths.MaxRecordId}.");
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

        private static bool TryGetCardIdFromPath(string path, out int cardId)
        {
            cardId = 0;
            var name = Path.GetFileNameWithoutExtension(path);
            return name.StartsWith("card_") && int.TryParse(name.Substring(5), out cardId);
        }

        private void HandleMetadataChanged()
        {
            MarkDirty();
            if (previewMode)
            {
                ShowCurrentCardPreview();
            }
        }

        private void HandleShapeChanged()
        {
            MarkDirty();
            UpdateStatistics();
            if (previewMode)
            {
                ShowCurrentCardPreview();
            }
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
