using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;

namespace FAUNA
{
    public static class FamiliarCache
    {
        // ─────────────────────────────────────────────────────────
        // Caches
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// All registered familiars, keyed by FamiliarId.
        /// Populated from Mods/CodysOasis.FAUNA/Familiars game asset.
        /// </summary>
        public static Dictionary<string, FamiliarData> Familiars { get; private set; } = new();

        /// <summary>
        /// Cached dialogue, keyed by DialogueAsset path.
        /// Loaded on demand via GameContent.
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> DialogueCache { get; set; } = new();

        // ─────────────────────────────────────────────────────────
        // Populate
        // ─────────────────────────────────────────────────────────

        public static void Build()
        {
            Clear();

            var asset = ModEntry.ModHelper.GameContent
                .Load<Dictionary<string, FamiliarData>>("Mods/CodysOasis.FAUNA/Familiars");

            foreach (var kvp in asset)
            {
                string familiarId = kvp.Key;
                FamiliarData data = kvp.Value;

                Familiars[familiarId] = data;

                // Load and cache sprite texture if asset path is set
                if (!string.IsNullOrEmpty(data.SpriteAsset))
                {
                    try
                    {
                        ModEntry.FamiliarTextures[familiarId] =
                            ModEntry.ModHelper.GameContent.Load<Texture2D>(data.SpriteAsset);
                    }
                    catch (Exception ex)
                    {
                        ModEntry.ModMonitor.Log(
                            $"[FamiliarCache] Failed to load sprite for {familiarId}: {ex.Message}",
                            LogLevel.Warn);
                    }
                }

                ModEntry.ModMonitor.Log(
                    $"[FamiliarCache] Registered: {familiarId} ({data.DisplayName})",
                    LogLevel.Trace);
            }

            // Sync to RegisteredFamiliars for anything that still references it
            ModEntry.RegisteredFamiliars = new Dictionary<string, FamiliarData>(Familiars);

            ModEntry.ModMonitor.Log(
                $"[FamiliarCache] Built cache: {Familiars.Count} familiar(s).",
                LogLevel.Debug);
        }

        // ─────────────────────────────────────────────────────────
        // Dialogue
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Gets dialogue for a familiar, loading from game asset if not cached.
        /// </summary>
        public static Dictionary<string, string>? GetDialogue(string familiarId)
        {
            if (!Familiars.TryGetValue(familiarId, out var data))
                return null;

            if (string.IsNullOrEmpty(data.DialogueAsset))
                return null;

            if (DialogueCache.TryGetValue(data.DialogueAsset, out var cached))
                return cached;

            try
            {
                var dialogue = ModEntry.ModHelper.GameContent
                    .Load<Dictionary<string, string>>(data.DialogueAsset);
                DialogueCache[data.DialogueAsset] = dialogue;
                return dialogue;
            }
            catch
            {
                ModEntry.ModMonitor.Log(
                    $"[FamiliarCache] Failed to load dialogue for {familiarId} at {data.DialogueAsset}",
                    LogLevel.Warn);
                return null;
            }
        }

        public static void ClearDialogueCache()
        {
            DialogueCache.Clear();
        }

        // ─────────────────────────────────────────────────────────
        // Clear
        // ─────────────────────────────────────────────────────────

        public static void Clear()
        {
            Familiars.Clear();
            DialogueCache.Clear();
            ModEntry.FamiliarTextures.Clear();

            ModEntry.ModMonitor.Log("[FamiliarCache] Cache cleared.", LogLevel.Debug);
        }
    }
}