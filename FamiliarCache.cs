using StardewModdingAPI;
using System.Collections.Generic;

namespace FAUNA
{
    /// <summary>
    /// Caches frequently accessed data to avoid repeated disk reads.
    /// All caches are populated on game launch and cleared on save unload.
    /// </summary>
    public static class FamiliarCache
    {
        // ─────────────────────────────────────────────────────────
        // Caches
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Caches familiars.json content per content pack UniqueID.
        /// Key: pack UniqueID, Value: list of FamiliarData
        /// </summary>
        public static Dictionary<string, List<FamiliarData>> FamiliarDataByPack { get; private set; } = new();

        /// <summary>
        /// Maps FamiliarId → owning IContentPack for fast lookup.
        /// </summary>
        public static Dictionary<string, IContentPack> PackByFamiliarId { get; private set; } = new();

        /// <summary>
        /// Caches dialogue JSON files.
        /// Key: pack UniqueID + ":" + dialogue path, Value: dialogue dictionary
        /// </summary>
        public static Dictionary<string, Dictionary<string, string>> DialogueByPath { get; private set; } = new();

        // ─────────────────────────────────────────────────────────
        // Populate
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Builds all caches from loaded content packs.
        /// Call after LoadContentPacks() completes.
        /// </summary>
        public static void Build()
        {
            Clear();

            foreach (var kvp in ModEntry.ContentPacks)
            {
                string packId = kvp.Key;
                IContentPack pack = kvp.Value;

                // Cache familiars.json
                var familiars = pack.ReadJsonFile<List<FamiliarData>>("familiars.json");
                if (familiars == null) continue;

                FamiliarDataByPack[packId] = familiars;

                // Cache PackByFamiliarId for fast reverse lookup
                foreach (var familiar in familiars)
                {
                    if (!string.IsNullOrEmpty(familiar.FamiliarId))
                        PackByFamiliarId[familiar.FamiliarId] = pack;
                }
            }

            ModEntry.ModMonitor.Log(
                $"[FamiliarCache] Built cache: {PackByFamiliarId.Count} familiars, " +
                $"{FamiliarDataByPack.Count} packs.",
                LogLevel.Debug);
        }

        /// <summary>
        /// Gets or caches a dialogue JSON file for a given pack and path.
        /// </summary>
        public static Dictionary<string, string>? GetDialogue(
            IContentPack pack, string dialoguePath)
        {
            string cacheKey = $"{pack.Manifest.UniqueID}:{dialoguePath}";

            if (DialogueByPath.TryGetValue(cacheKey, out var cached))
                return cached;

            try
            {
                var dialogue = pack.ReadJsonFile<Dictionary<string, string>>(dialoguePath);
                if (dialogue != null)
                    DialogueByPath[cacheKey] = dialogue;
                return dialogue;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the content pack that owns a given familiar ID.
        /// </summary>
        public static IContentPack? GetPackForFamiliar(string familiarId)
        {
            PackByFamiliarId.TryGetValue(familiarId, out var pack);
            return pack;
        }

        // ─────────────────────────────────────────────────────────
        // Clear
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Clears all caches. Call on save unload or content invalidation.
        /// </summary>
        public static void Clear()
        {
            FamiliarDataByPack.Clear();
            PackByFamiliarId.Clear();
            DialogueByPath.Clear();

            ModEntry.ModMonitor.Log(
                "[FamiliarCache] Cache cleared.",
                LogLevel.Debug);
        }
    }
}