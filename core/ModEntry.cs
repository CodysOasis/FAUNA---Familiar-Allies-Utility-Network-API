using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.BellsAndWhistles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.GameData.Buildings;
using StardewValley.GameData.Locations;
using StardewValley.ItemTypeDefinitions;
using StardewValley.GameData.BigCraftables;

namespace FAUNA

{
    public class ModEntry : Mod
    {
        public static Dictionary<string, FamiliarData> RegisteredFamiliars = new();
        public static Dictionary<string, Texture2D> FamiliarTextures = new();
        public static Dictionary<string, IContentPack> ContentPacks = new();
        public static FamiliarManager? FamiliarManager;
        public static FamiliarButton? FamiliarButton;
        public static ModConfig Config = new();
        public static IModHelper ModHelper = null!;
        public static IMonitor ModMonitor = null!;
        public static ITranslationHelper Translation = null!;
        private bool _pendingSpawn = false;


        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            // Seed empty familiars asset
            if (e.NameWithoutLocale.IsEquivalentTo("Mods/CodysOasis.FAUNA/Familiars"))
            {
                e.LoadFrom(
                    () => new Dictionary<string, FamiliarData>(StringComparer.OrdinalIgnoreCase),
                    AssetLoadPriority.Low);
                return;
            }

            // Seed empty shops asset
            if (e.NameWithoutLocale.IsEquivalentTo("Mods/CodysOasis.FAUNA/Shops"))
            {
                e.LoadFrom(
                    () => new Dictionary<string, FamiliarShopData>(StringComparer.OrdinalIgnoreCase),
                    AssetLoadPriority.Low);
                return;
            }
            // Tilesheet
            if (e.NameWithoutLocale.IsEquivalentTo("Maps/FamiliarDenTilesheet"))
            {
                e.LoadFromModFile<Texture2D>("assets/maps/tilesheets/FamiliarDenTilesheet.png",
                    AssetLoadPriority.Medium);
            }

            // Building texture
            if (e.NameWithoutLocale.IsEquivalentTo("Buildings/FAUNA.FamiliarDen"))
            {
                e.LoadFromModFile<Texture2D>("assets/buildings/FamiliarDen.png",
                    AssetLoadPriority.Medium);
            }

            // Building data
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Buildings"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, BuildingData>().Data;
                    data["FAUNA.FamiliarDen"] = DenDataBuilder.BuildData();
                }, AssetEditPriority.Default);
            }

            // Location data
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Locations"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, LocationData>().Data;
                    data["FAUNA.FamiliarDen"] = DenDataBuilder.BuildLocationData();
                }, AssetEditPriority.Default);
            }

            // Map — stitched dynamically
            if (e.NameWithoutLocale.IsEquivalentTo("Maps/FAUNA.FamiliarDen"))
            {
                e.LoadFrom(() => DenMapBuilder.BuildMap(
                    Helper, RegisteredFamiliars,
                    FamiliarManager?.OwnedFamiliars.Count ?? 0),
                    AssetLoadPriority.Exclusive);
            }
            // OuijaBoard 
            if (e.NameWithoutLocale.IsEquivalentTo("assets/BigCraftables/ouija_board"))
            {
                e.LoadFromModFile<Texture2D>("assets/BigCraftables/ouija_board.png", AssetLoadPriority.Medium);
            }
            if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, StardewValley.GameData.BigCraftables.BigCraftableData>().Data;

                    data["FAUNA_OuijaBoard"] = new StardewValley.GameData.BigCraftables.BigCraftableData
                    {
                        Name = "Ouija Board",
                        DisplayName = "Ouija Board",
                        Description = "A mysterious board used to contact familiar spirits...",
                        Price = 250,
                        Texture = "assets/BigCraftables/ouija_board",
                        SpriteIndex = 0,
                        
                    };
                });
            }
            if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;

                    // Format: "ingredients/Home/(BC)ItemId/true/default/"
                    // Ingredients: "itemId quantity itemId quantity..."
                    // Wood=388, Stone=390, Void Essence=769, Solar Essence=768

                    data["FAUNA_OuijaBoard"] = "388 20 769 5 768 5/Home/(BC)FAUNA_OuijaBoard/true/default/";
                });
            }

            //mail
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Mail"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;
                    data["FAUNA_OuijaLetter"] = 
                        "Something arrived for you overnight. No return address. "
                        + "No signature. Just a folded slip of paper and the faint "
                        + "smell of something you can't quite place...^^"
                        + "''You already know how to find them.''^^"
                        + "''You just need the right door.''%item craftingRecipe FAUNA_OuijaBoard %%";
                });
            }
        }

        private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
        {
            if (e.NamesWithoutLocale.Any(n =>
                n.IsEquivalentTo("Mods/CodysOasis.FAUNA/Familiars") ||
                n.IsEquivalentTo("Mods/CodysOasis.FAUNA/Shops")))
            {
                FamiliarCache.Build();

                // Reload shops from CP asset
                FamiliarShopRegistry.Clear(); 
                var shops = Helper.GameContent.Load<Dictionary<string, FamiliarShopData>>(
                    "Mods/CodysOasis.FAUNA/Shops");
                foreach (var kvp in shops)
                    FamiliarShopRegistry.RegisterShop(kvp.Value);
                RegisterDefaultShop();

                Monitor.Log("[FAUNA] Assets invalidated — rebuilt familiar and shop cache.", LogLevel.Debug);
            }

            // Clear dialogue cache if any dialogue asset was invalidated
            if (e.NamesWithoutLocale.Any(n => n?.ToString()?.Contains("/Dialogue/") == true))
            {
                FamiliarCache.ClearDialogueCache();
                Monitor.Log("[FAUNA] Dialogue assets invalidated — cleared dialogue cache.", LogLevel.Debug);
            }
        }
       public override void Entry(IModHelper helper)
        {
            ModHelper = helper;
            Monitor.Log("Entry() called - this is the latest build!", LogLevel.Info);
            ModMonitor = Monitor;

            Config = helper.ReadConfig<ModConfig>();
            FamiliarManager = new FamiliarManager(helper, Monitor);
            FamiliarButton = new FamiliarButton(helper, Monitor);
            Translation = Helper.Translation;

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            Helper.Events.GameLoop.Saving += OnSaving;
            Helper.Events.GameLoop.Saved += OnSaved;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.Saved += OnSaved;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Player.Warped += OnWarped;
            helper.ConsoleCommands.Add("ff_reset", "Resets all familiar save data.", (cmd, args) =>
        {
            FamiliarManager!.ResetAllFamiliars();
            Monitor.Log("Familiar save data cleared — reload your save.", LogLevel.Warn);
        });
        
        }
        public override object? GetApi()
        {
            return new FaunaApi(FamiliarManager!);
        }

        
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Defer asset load one tick so CP packs have registered their patches
            Helper.Events.GameLoop.UpdateTicking += OnFirstTick;
            Translation = Helper.Translation;
            HarmonyPatches.Apply(ModManifest.UniqueID);
            SetupGMCM();

            // Register custom tile action for FAUNA familiar shops
            GameLocation.RegisterTileAction("FAUNA.OpenFamiliarShop", (location, args, player, tile) =>
            {

                if (args.Length < 2) return false;
                string shopId = args[1];

                var shop = FamiliarShopRegistry.GetShop(shopId);
                if (shop == null)
                {
                    ModEntry.ModMonitor.Log(
                        $"[ShopTile] No shop found with ID '{shopId}'",
                        LogLevel.Warn);
                    return false;
                }

                Game1.activeClickableMenu = new FamiliarShopMenu(shop);
                return true;
            });
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            FamiliarCache.Clear();
        }
        private void RegisterDefaultShop()
        {
            // Default shop stock is built dynamically from the game asset
            // so it always reflects whatever CP packs have registered
            FamiliarShopRegistry.RegisterShop(new FamiliarShopData
            {
                ShopId = "ouija_default",
                DisplayName = "Spirit Familiar Summoning",
                Stock = new List<FamiliarShopEntry>() // populated at shop-open time
            });
        }
        private void SetupGMCM()
        {
            var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(
                "spacechase0.GenericModConfigMenu");

            if (api == null)
                return; // GMCM not installed — skip silently

            api.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => Config.ShowButton,
                setValue: val => Config.ShowButton = val,
                name: () => "Show Journal Button",
                tooltip: () => "Show the Familiar Journal button on the HUD."
            );

            api.AddKeybindList(
                mod: ModManifest,
                getValue: () => Config.JournalKey,
                setValue: val => Config.JournalKey = val,
                name: () => "Journal Keybind",
                tooltip: () => "Press this key to open the Familiar Journal."
            );
        }
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            FamiliarManager!.Load();
            Helper.GameContent.InvalidateCache("Maps/FAUNA.FamiliarDen");
            _pendingSpawn = true;
            
        }
       private void OnFirstTick(object? sender, UpdateTickingEventArgs e)
        {
            Helper.Events.GameLoop.UpdateTicking -= OnFirstTick;
            // Don't load assets here — CP hasn't patched them yet
            // OnAssetsInvalidated handles the real load after CP fires
            RegisterDefaultShop();
            Monitor.Log($"FAUNA loaded {RegisteredFamiliars.Count} familiar(s) total.", LogLevel.Info);
        }
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (_pendingSpawn && Context.IsWorldReady && Game1.currentLocation != null)
            {
                // Wait a full second (60 ticks) after world ready before spawning
                if (e.IsMultipleOf(60))
                {
                    _pendingSpawn = false;
                    FamiliarManager!.SpawnFamiliars();

                }
            }
        }
        private void OnWarped(object? sender, WarpedEventArgs e)
{
    if (FamiliarManager != null)
    {
        var followingFamiliars = e.OldLocation?.characters
            .OfType<FamiliarEntity>()
            .Where(f => f.CurrentState == FamiliarState.Following)
            .ToList();

        if (followingFamiliars != null)
        {
            foreach (var familiar in followingFamiliars)
            {
                e.OldLocation!.characters.Remove(familiar);
                familiar.currentLocation = e.NewLocation;

                // Use the familiar's own FindOpenTileNear so it uses
                // the same IsTileBlocked logic as movement
                familiar.Position = familiar.FindOpenTileNear(e.NewLocation, e.Player.Position);

                // Clear stale path from old location
                familiar.ClearPath();

                e.NewLocation.characters.Add(familiar);
            }
        }
    }

            // Then handle den spawn as before
            if (e.NewLocation?.Name == "FAUNA.FamiliarDen")
                FamiliarManager!.SpawnFamiliars();
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            // Despawn first so FamiliarEntity never touches the XML serializer
            FamiliarManager!.DespawnFamiliars();
            // Then save our custom data
            FamiliarManager!.Save();
        }

        private void OnSaved(object? sender, SavedEventArgs e)
        {
            // Respawn after save completes
            FamiliarManager!.SpawnFamiliars();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Game1.player.mailReceived.Contains("FAUNA_OuijaLetter"))
                {
                    Game1.player.mailbox.Add("FAUNA_OuijaLetter");
                }
            FamiliarManager!.ApplyDailyDecay();
            
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (!e.Button.IsActionButton())
                return;

        var tile = Game1.currentCursorTile;

            var obj = Game1.currentLocation.getObjectAtTile(
                (int)Game1.currentCursorTile.X,
                (int)Game1.currentCursorTile.Y
            );

            if (obj == null)
                return;

            if (obj.Name == "Ouija Board")
            {
                var shop = FamiliarShopRegistry.GetShop("ouija_default");

                if (shop == null)
                {
                    Game1.addHUDMessage(new HUDMessage(
    ModEntry.Translation.Get("UI.Shop.SilentSpirits").ToString(), 3));
                    return;
                }

                Game1.activeClickableMenu = new FamiliarShopMenu(shop);
            }
        }

        private Vector2 FindOpenTileNear(GameLocation location, Vector2 center)
        {
            int centerTileX = (int)(center.X / 64f);
            int centerTileY = (int)(center.Y / 64f);

            for (int radius = 1; radius <= 5; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                            continue;

                        int checkX = centerTileX + dx;
                        int checkY = centerTileY + dy;

                        if (checkX < 0 || checkY < 0 ||
                            checkX >= location.map.Layers[0].LayerSize.Width ||
                            checkY >= location.map.Layers[0].LayerSize.Height)
                            continue;

                        if (location.isTilePassable(
                            new xTile.Dimensions.Location(checkX, checkY),
                            Game1.viewport))
                            return new Vector2(checkX * 64f, checkY * 64f);
                    }
                }
            }
            return center + new Vector2(64f, 0f);
        }
    }
}