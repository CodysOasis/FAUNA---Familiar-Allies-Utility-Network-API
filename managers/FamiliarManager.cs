using StardewModdingAPI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Buildings;
using System.Linq;
using System.Text.Json;

namespace FAUNA
{
    // FamiliarManager is the runtime brain of the framework.
    // It holds the player's active familiars and handles saving/loading them.
    public class FamiliarManager
    {

        private const string SaveKey = "FamiliarInstances";
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;

        // All familiars currently owned by the player
        public List<FamiliarInstance> OwnedFamiliars { get; private set; } = new();

        public FamiliarManager(IModHelper helper, IMonitor monitor)
        {
            _helper = helper;
            _monitor = monitor;
        }


        //Diet checker to see what items are valid Food
        public static bool IsAcceptableFood(Item item, DietType diet)
        {
            if (item is not StardewValley.Object obj)
                return false;

            int cat = obj.Category;

            bool isEggOrMilk  = cat == -5 || cat == -6;
            bool isFish  = cat == -4 ;
            bool isFruitOrVeg = cat == -75 || cat == -79;
            bool isBugMeat = obj.ItemId == "684" || obj.ItemId == "874";

            // Check for Animal Husbandry meat via context tags
            bool isMeat = false;
            bool animalHusbandryLoaded = ModEntry.ModHelper.ModRegistry.IsLoaded("DIGUS.AnimalHusbandryMod");
            if (animalHusbandryLoaded)
            {
                // AHM meat items have category -75 OR are cooking items;
                // most reliable: check context tags for "meat_item" if AHM adds them,
                // otherwise fall back to checking item name
                isMeat = item.HasContextTag("meat_item") 
                    || item.Name.EndsWith("Meat", StringComparison.OrdinalIgnoreCase);
            }

            return diet switch
            {
                DietType.Carnivore => isEggOrMilk || isMeat || isFish || isBugMeat,
                DietType.Herbivore => isFruitOrVeg,
                DietType.Omnivore  => isEggOrMilk || isFruitOrVeg || isMeat || isBugMeat ||isFish,
                _ => false
            };
        }

        // Call this when a save is loaded
public void Load()
{
    // Try modData key (0.1.1+)
    if (Game1.player.modData.TryGetValue(SaveKey, out string? json)
        && !string.IsNullOrEmpty(json))
    {
        OwnedFamiliars = JsonSerializer.Deserialize<List<FamiliarInstance>>(json) ?? new();
        _monitor.Log($"Loaded {OwnedFamiliars.Count} familiar instance(s) from save.", LogLevel.Info);
        return;
    }

    // Nothing found
    OwnedFamiliars = new();
    _monitor.Log("No familiar save data found, starting fresh.", LogLevel.Info);
}
        // Call this when the game is about to save
        public void Save()
{
    Game1.player.modData[SaveKey] = JsonSerializer.Serialize(OwnedFamiliars);
}
        // Call before saving — removes FamiliarEntity instances from all locations
public void DespawnFamiliars(bool force = false)
{
    foreach (var location in Game1.locations)
    {
        int removed = 0;
        location.characters.RemoveWhere(c => {
            if (c is FamiliarEntity fe && 
                (force || fe.CurrentState != FamiliarState.Following))
            { removed++; return true; }
            return false;
        });

        // This must be OUTSIDE the if block!
        foreach (var building in location.buildings)
        {
            GameLocation? interior = building.GetIndoors();
            if (interior == null) continue;
            int removedInterior = 0;
            interior.characters.RemoveWhere(c => {
                if (c is FamiliarEntity fe && 
                    (force || fe.CurrentState != FamiliarState.Following))
                { removedInterior++; return true; }
                return false;
            });
            if (removedInterior > 0)
                _monitor.Log($"Removed {removedInterior} familiar(s) from {interior.Name}", LogLevel.Trace);
        }

        if (removed > 0)
            _monitor.Log($"Removed {removed} familiar(s) from {location.Name}", LogLevel.Trace);
    }
}

        // Add a new familiar by ID (called when player acquires one)
        public FamiliarInstance? AddFamiliar(string familiarId, string customName = "")
{
    if (!ModEntry.RegisteredFamiliars.ContainsKey(familiarId))
    {
        _monitor.Log($"Tried to add unknown familiar ID: {familiarId}", LogLevel.Warn);
        return null;
    }

    var instance = new FamiliarInstance
    {
        FamiliarId  = familiarId,
        InstanceId  = Guid.NewGuid().ToString(),
        CustomName  = customName
    };

    OwnedFamiliars.Add(instance);
    ModEntry.ModHelper.GameContent.InvalidateCache("Maps/FAUNA.FamiliarDen");
    return instance;
}

        // Decay needs at the start of each day
public void ApplyDailyDecay()
{
    foreach (var instance in OwnedFamiliars)
    {
        if (!ModEntry.RegisteredFamiliars.TryGetValue(
            instance.FamiliarId, out var data)) continue;

        instance.Food      = Math.Max(0f, instance.Food - data.Needs.FoodDecayPerDay);
        instance.Attention = Math.Max(0f, instance.Attention - data.Needs.AttentionDecayPerDay);

        instance.FedToday    = false;
        instance.PettedToday = false;
        instance.GiftedToday = false;
        instance.SaidTodayLines.Clear();
        instance.FriendshipGivenToday.Clear();

    }

    // Trigger day start abilities once per entity — outside the instance loop
    foreach (var loc in Game1.locations)
    {
        foreach (var character in loc.characters.OfType<FamiliarEntity>())
            character.TriggerDayStartAbilities();
    }
}
        
 public void SpawnFamiliars()
{
    
    DespawnFamiliars();

    GameLocation? interior = Game1.getLocationFromName("FAUNA.FamiliarDen");
    if (interior == null)
    {
        _monitor.Log("Den interior not found — spawn aborted.", LogLevel.Warn);
        return;
    }

    if (interior.map == null)
    {
        _monitor.Log("Den interior map not loaded — spawn aborted.", LogLevel.Warn);
        return;
    }

    Farm farm = Game1.getFarm();
    Building? den = farm.buildings.FirstOrDefault(b =>
        b.buildingType.Value == "FAUNA.FamiliarDen");

    if (den == null)
    {
        _monitor.Log("No Familiar Den found on farm — familiars will not spawn.", LogLevel.Warn);
        return;
    }

    int[] baseRoomX = { 2, 9, 16 };
    int roomY = 4;

    for (int i = 0; i < OwnedFamiliars.Count; i++)
    {
        var instance = OwnedFamiliars[i];

        //dont respawn following familiars
        bool alreadyFollowing = Game1.locations
        .SelectMany(l => l.characters.OfType<FamiliarEntity>())
        .Concat(Game1.locations
            .SelectMany(l => l.buildings)
            .Select(b => b.GetIndoors())
            .Where(l => l != null)
            .SelectMany(l => l!.characters.OfType<FamiliarEntity>()))
        .Any(e => e.InstanceId == instance.InstanceId 
            && e.CurrentState == FamiliarState.Following);

    if (alreadyFollowing) continue;

        if (!ModEntry.RegisteredFamiliars.TryGetValue(instance.FamiliarId, out var data))
        {
            _monitor.Log($"Could not find data for familiar ID: {instance.FamiliarId}", LogLevel.Warn);
            continue;
        }

        int spawnTileX = i < 3
            ? baseRoomX[i]
            : baseRoomX[2] + (i - 2) * 7;

        Vector2 spawnPosition = new Vector2(spawnTileX * 64f, roomY * 64f);


FamiliarEntity entity;
try
{
    entity = new FamiliarEntity(instance.FamiliarId, data, instance, spawnPosition, interior);
}
catch (Exception ex)
{
    _monitor.Log($"[SpawnError] Failed to create entity for {instance.FamiliarId}: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
    continue;
}  
        // Load portrait if defined
        if (!string.IsNullOrEmpty(data.PortraitAsset))
        {
            try
            {
                entity.Portrait = ModEntry.ModHelper.GameContent.Load<Texture2D>(data.PortraitAsset);
            }
            catch
            {
                _monitor.Log($"Could not load portrait for {instance.FamiliarId}", LogLevel.Warn);
            }
        }

try
{
    interior.characters.Add(entity);
}
catch (Exception ex)
{
    _monitor.Log($"[SpawnError] Failed to add entity to location: {ex.Message}", LogLevel.Error);
}
  }
}
public void UpdateFollowIndices()
{
    int index = 0;
    foreach (var instance in OwnedFamiliars)
    {
        // Find the entity for this instance
        var entity = GetEntityForInstance(instance.InstanceId);
        if (entity != null && entity.CurrentState == FamiliarState.Following)
        {
            entity.FollowIndex = index;
            index++;
        }
    }
}

private FamiliarEntity? GetEntityForInstance(string instanceId)
{
    foreach (var location in Game1.locations)
    {
        var entity = location.characters
            .OfType<FamiliarEntity>()
            .FirstOrDefault(e => e.InstanceId == instanceId);
        if (entity != null)
        {
            return entity;
        }

        foreach (var building in location.buildings)
        {
            var interior = building.GetIndoors();
            if (interior == null) continue;
            entity = interior.characters
                .OfType<FamiliarEntity>()
                .FirstOrDefault(e => e.InstanceId == instanceId);
            if (entity != null)
            {
                return entity;
            }
        }
    }

    return null;
}
public void ResetAllFamiliars()
{
    OwnedFamiliars.Clear();
    _helper.Data.WriteSaveData<List<FamiliarInstance>>("FamiliarInstances", null);
    _monitor.Log("All familiar data reset.", LogLevel.Warn);
}
    }
}