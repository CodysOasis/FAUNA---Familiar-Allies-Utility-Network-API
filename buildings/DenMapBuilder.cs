using xTile;
using xTile.Layers;
using xTile.Tiles;
using xTile.Dimensions;
using StardewModdingAPI;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FAUNA
{
    public static class DenMapBuilder
    {
        // Row ranges (in tiles) — matches your 22x22 base map
        private const int RoomRowStart      = 0;
        private const int RoomRowEnd        = 9;   // inclusive, 10 rows tall
        private const int HallwayRowStart   = 10;
        private const int HallwayRowEnd     = 12;  // inclusive, 3 rows tall
        private const int HubRowStart       = 13;
        private const int HubRowEnd         = 19;  // inclusive, 7 rows tall
        private const int BaseMapWidth      = 22;
        private const int MapHeight         = 22;  // stays fixed always
        private const int ColumnWidth       = 7;   // tiles per extension column
        private const int BaseFamiliarSlots = 3;   // rooms in the base map

        public static Map BuildMap(
            IModHelper helper,
            Dictionary<string, FamiliarData> registeredFamiliars,
            int ownedCount)
        {
            Map map = helper.ModContent.Load<Map>("assets/maps/FamiliarDen.tmx");
            var ownedFamiliars = ModEntry.FamiliarManager!.OwnedFamiliars;
            int extraColumns = System.Math.Max(0, ownedCount - BaseFamiliarSlots);
            int[] baseRoomX = { 1, 8, 15 };

            // ── Pass 1: base slots ──────────────────────────────────────
            for (int i = 0; i < System.Math.Min(ownedCount, BaseFamiliarSlots); i++)
            {
                if (i >= ownedFamiliars.Count) break;
                var instance = ownedFamiliars[i];

                if (!registeredFamiliars.TryGetValue(instance.FamiliarId, out var data)) continue;
                if (string.IsNullOrEmpty(data.RoomAsset)) continue;

                try
                {
                    var customRoom = helper.GameContent.Load<Map>(data.RoomAsset);
                    CopyChunk(customRoom, map, baseRoomX[i], RoomRowStart);
                }
                catch { }
            }

            // ── Load extension chunks ───────────────────────────────────
            Map roomDefault  = helper.ModContent.Load<Map>("assets/maps/DefaultRoom.tmx");
            Map hallwayChunk = helper.ModContent.Load<Map>("assets/maps/HallwayExtension.tmx");
            Map hubChunk     = helper.ModContent.Load<Map>("assets/maps/HubExtension.tmx");
            Map rightCap     = helper.ModContent.Load<Map>("assets/maps/RightCap.tmx");

            if (extraColumns == 0)
            {
                int newBaseWidth = BaseMapWidth + 1;
                foreach (var layer in map.Layers)
                    ResizeLayer(map, layer, newBaseWidth, MapHeight);
                CopyChunk(rightCap, map, BaseMapWidth, 0);
                return map;
            }

            // ── Pass 2: extension columns ───────────────────────────────
            int newWidth = BaseMapWidth + extraColumns * ColumnWidth + 1;
            foreach (var layer in map.Layers)
                ResizeLayer(map, layer, newWidth, MapHeight);

            var extraOwned = ownedFamiliars.Skip(BaseFamiliarSlots).ToList();

            for (int col = 0; col < extraColumns; col++)
            {
                int tileOffsetX = BaseMapWidth + col * ColumnWidth;
                var instance = extraOwned[col];

                Map roomToUse = roomDefault;

                if (registeredFamiliars.TryGetValue(instance.FamiliarId, out var data)
                    && !string.IsNullOrEmpty(data.RoomAsset))
                {
                    try
                    {
                        roomToUse = helper.GameContent.Load<Map>(data.RoomAsset);
                    }
                    catch { }
                }

                CopyChunk(roomToUse,    map, tileOffsetX, RoomRowStart);
                CopyChunk(hallwayChunk, map, tileOffsetX, HallwayRowStart);
                CopyChunk(hubChunk,     map, tileOffsetX, HubRowStart);
            }

            int capOffsetX = BaseMapWidth + extraColumns * ColumnWidth;
            CopyChunk(rightCap, map, capOffsetX, 0);
            return map;
        }
        // ─────────────────────────────────────────────────────────
        // Resize a layer to a new width by rebuilding its tile array
        // ─────────────────────────────────────────────────────────
        private static void ResizeLayer(Map map, Layer layer, int newWidth, int height)
        {
            // Cache existing tiles before we touch anything
            var cached = new Tile?[layer.LayerSize.Width, layer.LayerSize.Height];
            for (int x = 0; x < layer.LayerSize.Width; x++)
                for (int y = 0; y < layer.LayerSize.Height; y++)
                    cached[x, y] = layer.Tiles[x, y];

            // Resize via the layer's own method
            layer.LayerSize = new Size(newWidth, height);

            // Restore cached tiles
            for (int x = 0; x < cached.GetLength(0); x++)
                for (int y = 0; y < cached.GetLength(1); y++)
                    layer.Tiles[x, y] = cached[x, y];
        }

        // ─────────────────────────────────────────────────────────
        // Copy all layers from a source chunk into the target map
        // at a given tile offset
        // ─────────────────────────────────────────────────────────
        private static void CopyChunk(Map source, Map target,
            int offsetX, int offsetY)
        {
            foreach (var sourceLayer in source.Layers)
            {
                // Find matching layer in target by name
                var targetLayer = target.Layers
                    .FirstOrDefault(l => l.Id == sourceLayer.Id);

                if (targetLayer == null)
                    continue; // skip layers that don't exist in target

                for (int x = 0; x < sourceLayer.LayerSize.Width; x++)
                {
                    for (int y = 0; y < sourceLayer.LayerSize.Height; y++)
                    {
                        var sourceTile = sourceLayer.Tiles[x, y];
                        if (sourceTile == null)
                            continue;

                        // Remap the tilesheet reference to the target map's sheets
                        TileSheet? targetSheet = target.GetTileSheet(
                            sourceTile.TileSheet.Id);

                        if (targetSheet == null)
                            continue; // tilesheet not merged yet — skip

                        targetLayer.Tiles[offsetX + x, offsetY + y] =
                            new StaticTile(targetLayer, targetSheet,
                                BlendMode.Alpha, sourceTile.TileIndex);
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────
        // Merge an extra tilesheet into the map if not already present
        // ─────────────────────────────────────────────────────────
        private static void MergeTilesheet(Map map, string assetPath, IModHelper helper)
        {
            string sheetId = Path.GetFileNameWithoutExtension(assetPath);

            if (map.GetTileSheet(sheetId) != null)
                return; // already present

            var texture = helper.ModContent.Load<Texture2D>(assetPath);
            var sheet   = new TileSheet(sheetId, map, assetPath,
                new Size(texture.Width / 16, texture.Height / 16),
                new Size(16, 16));

            map.AddTileSheet(sheet);
            map.LoadTileSheets(Game1.mapDisplayDevice);
        }
    }
}