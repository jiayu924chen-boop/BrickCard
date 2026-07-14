using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CardGame.Editors
{
    [Serializable]
    public struct GridPoint
    {
        public int x;
        public int y;

        public GridPoint(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    [Serializable]
    public sealed class BoardShapeData
    {
        public int schemaVersion = 1;
        public int id;
        public int columns = 10;
        public int rows = 8;
        public List<GridPoint> cells = new List<GridPoint>();
    }

    [Serializable]
    public sealed class BoardShapeDatabase
    {
        public int schemaVersion = 1;
        public List<BoardShapeData> boards = new List<BoardShapeData>();
    }

    [Serializable]
    public sealed class CardShapeData
    {
        public int schemaVersion = 1;
        public int id;
        public int columns = 8;
        public int rows = 8;
        public string name = string.Empty;
        public string effect = string.Empty;
        public string description = string.Empty;
        public string notes = string.Empty;
        public List<GridPoint> cells = new List<GridPoint>();
    }

    public static class ShapeEditorPaths
    {
        public const int MaxGridSize = 99;
        public const int MaxRecordId = 999999;

        public static string BoardDatabasePath => Path.Combine(EditorDataRoot, "boards.json");

        public static string CardDatabaseDirectory => Path.Combine(EditorDataRoot, "cards");

        public static string EditorDataRoot
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(Application.dataPath, "StreamingAssets", "CardGameEditorData");
#else
                return Path.Combine(Application.persistentDataPath, "CardGameEditorData");
#endif
            }
        }

        public static string GetCardPath(int cardId)
        {
            return Path.Combine(CardDatabaseDirectory, $"card_{cardId:000}.json");
        }

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(EditorDataRoot);
            Directory.CreateDirectory(CardDatabaseDirectory);

#if !UNITY_EDITOR
            CopyPackagedDataIfMissing();
#endif
        }

#if !UNITY_EDITOR
        private static void CopyPackagedDataIfMissing()
        {
            var packagedRoot = Path.Combine(Application.streamingAssetsPath, "CardGameEditorData");
            var packagedBoards = Path.Combine(packagedRoot, "boards.json");
            if (!File.Exists(BoardDatabasePath) && File.Exists(packagedBoards))
            {
                File.Copy(packagedBoards, BoardDatabasePath);
            }

            var packagedCards = Path.Combine(packagedRoot, "cards");
            if (!Directory.Exists(packagedCards))
            {
                return;
            }

            foreach (var sourcePath in Directory.GetFiles(packagedCards, "card_*.json"))
            {
                var destinationPath = Path.Combine(CardDatabaseDirectory, Path.GetFileName(sourcePath));
                if (!File.Exists(destinationPath))
                {
                    File.Copy(sourcePath, destinationPath);
                }
            }
        }
#endif
    }

    public static class ShapeEditorDataUtility
    {
        public static void Normalize(BoardShapeData board)
        {
            if (board == null)
            {
                return;
            }

            NormalizeShape(ref board.columns, ref board.rows, ref board.cells, 10, 8);
        }

        public static void Normalize(CardShapeData card)
        {
            if (card == null)
            {
                return;
            }

            NormalizeShape(ref card.columns, ref card.rows, ref card.cells, 8, 8);
            card.name = card.name ?? string.Empty;
            card.effect = card.effect ?? string.Empty;
            card.description = card.description ?? string.Empty;
            card.notes = card.notes ?? string.Empty;
        }

        private static void NormalizeShape(ref int columns, ref int rows, ref List<GridPoint> cells, int defaultColumns, int defaultRows)
        {
            cells = cells ?? new List<GridPoint>();

            var maxX = -1;
            var maxY = -1;
            for (var i = 0; i < cells.Count; i++)
            {
                maxX = Mathf.Max(maxX, cells[i].x);
                maxY = Mathf.Max(maxY, cells[i].y);
            }

            columns = Mathf.Clamp(columns > 0 ? columns : Mathf.Max(defaultColumns, maxX + 1), 1, ShapeEditorPaths.MaxGridSize);
            rows = Mathf.Clamp(rows > 0 ? rows : Mathf.Max(defaultRows, maxY + 1), 1, ShapeEditorPaths.MaxGridSize);

            var unique = new HashSet<int>();
            var normalized = new List<GridPoint>(cells.Count);
            for (var i = 0; i < cells.Count; i++)
            {
                var point = cells[i];
                if (point.x < 0 || point.x >= columns || point.y < 0 || point.y >= rows)
                {
                    continue;
                }

                var key = point.y * ShapeEditorPaths.MaxGridSize + point.x;
                if (unique.Add(key))
                {
                    normalized.Add(point);
                }
            }

            normalized.Sort((left, right) => left.y != right.y ? left.y.CompareTo(right.y) : left.x.CompareTo(right.x));
            cells = normalized;
        }
    }

    public static class ShapeEditorFileStore
    {
        public static bool TryReadJson<T>(string path, out T value, out string error) where T : class
        {
            value = null;
            error = string.Empty;

            try
            {
                if (!File.Exists(path))
                {
                    return true;
                }

                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    error = $"Data file is empty: {Path.GetFileName(path)}";
                    return false;
                }

                value = JsonUtility.FromJson<T>(json);
                if (value == null)
                {
                    error = $"Data file is invalid: {Path.GetFileName(path)}";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not read {Path.GetFileName(path)}: {exception.Message}";
                return false;
            }
        }

        public static bool TryWriteJson<T>(string path, T value, out string error)
        {
            error = string.Empty;
            var temporaryPath = path + ".tmp";
            var backupPath = path + ".bak";

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(temporaryPath, JsonUtility.ToJson(value, true));
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temporaryPath, path, backupPath, true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(path, backupPath, true);
                        File.Delete(path);
                        File.Move(temporaryPath, path);
                    }

                    TryDeleteFile(backupPath);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not save {Path.GetFileName(path)}: {exception.Message}";
                return false;
            }
            finally
            {
                try
                {
                    TryDeleteFile(temporaryPath);
                }
                catch (Exception)
                {
                    // A stale temporary file is harmless and can be replaced on the next save.
                }
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
