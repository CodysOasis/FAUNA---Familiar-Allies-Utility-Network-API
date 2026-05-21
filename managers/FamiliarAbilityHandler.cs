using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Monsters;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FAUNA
{
    /// <summary>
    /// Handles processing and execution of familiar abilities.
    /// </summary>
    public class FamiliarAbilityHandler
    {
        // ─────────────────────────────────────────────────────────
        // Fields
        // ─────────────────────────────────────────────────────────

        private readonly FamiliarEntity _entity;
        private readonly FamiliarInstance _instance;
        private readonly FamiliarData _data;

        // Tracks cooldown timers per ability ID
        // Key: ability Id, Value: remaining cooldown in seconds
        private readonly Dictionary<string, float> _cooldowns = new();

        // Tracks currently active buff IDs so we can remove them
        private readonly HashSet<string> _activeBuffIds = new();

        private Vector2 _nearestWaterTile = Vector2.Zero;

        // ─────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────

        public FamiliarAbilityHandler(
            FamiliarEntity entity,
            FamiliarInstance instance,
            FamiliarData data)
        {
            _entity   = entity;
            _instance = instance;
            _data     = data;
        }

        // ─────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called every update tick while familiar is following.
        /// </summary>
        public void UpdateFollowing(GameTime time, GameLocation location)
        {
            var abilities = GetCurrentAbilities();
            if (abilities == null || abilities.Count == 0) return;

            float deltaSeconds = (float)time.ElapsedGameTime.TotalSeconds;



            foreach (var ability in abilities)
            {

                
                // Tick down cooldown
                if (_cooldowns.TryGetValue(ability.Id, out float remaining))
                {
                    _cooldowns[ability.Id] = remaining - deltaSeconds;
                    if (_cooldowns[ability.Id] > 0) continue;
                }

                // Check GSQ condition
                if (!string.IsNullOrEmpty(ability.Condition) &&
                    !GameStateQuery.CheckConditions(ability.Condition))
                    continue;

                // Check proc type
                switch (ability.Proc)
                {
                    case "OnFollow":
                        ExecuteAbility(ability, location);
                        break;
                     case "OnNearby":
            if (HasNearbyTarget(ability, location))
                ExecuteAbility(ability, location);
            break;
                }
            }
        }

        public void TickCooldowns(GameTime time)
        {
            float deltaSeconds = (float)time.ElapsedGameTime.TotalSeconds;
            var abilities = GetCurrentAbilities();
            if (abilities == null) return;

            foreach (var ability in abilities)
            {
                if (_cooldowns.TryGetValue(ability.Id, out float remaining))
                    _cooldowns[ability.Id] = remaining - deltaSeconds;
            }
        }

        /// <summary>
        /// Called when familiar enters Following state.
        /// Applies persistent buffs.
        /// </summary>
        public void OnStartFollowing()
        {
            var abilities = GetCurrentAbilities();
            if (abilities == null) return;

            foreach (var ability in abilities)
            {
                if (ability.Proc != "OnFollow") continue;
                if (!string.IsNullOrEmpty(ability.Condition) &&
                    !GameStateQuery.CheckConditions(ability.Condition))
                    continue;

                if (ability.AbilityClass == "Buff")
                    ApplyBuff(ability);
            }
        }

        /// <summary>
        /// Called when familiar leaves Following state.
        /// Removes all active buffs.
        /// </summary>
        public void OnStopFollowing()
        {
            RemoveAllBuffs();
        }

        /// <summary>
        /// Called once per day to handle OnDayStart abilities.
        /// </summary>
        public void OnDayStart()
        {
            var abilities = GetCurrentAbilities();
            if (abilities == null) return;

            foreach (var ability in abilities)
            {
                if (ability.Proc != "OnDayStart") continue;
                if (!string.IsNullOrEmpty(ability.Condition) &&
                    !GameStateQuery.CheckConditions(ability.Condition))
                    continue;

                ExecuteAbility(ability, _entity.currentLocation);
            }
        }

        public void OnAbilityWalkCompleted()
        {
            // Reset all OnNearby cooldowns so they restart properly
            var abilities = GetCurrentAbilities();
            if (abilities == null) return;

            foreach (var ability in abilities)
            {
                if (ability.Proc == "OnNearby")
                    _cooldowns[ability.Id] = ability.ProcTimer;
            }
        }

        // ─────────────────────────────────────────────────────────
        // Ability Execution
        // ─────────────────────────────────────────────────────────

        private void ExecuteAbility(AbilityData ability, GameLocation location)
        {
            // Reset cooldown
            _cooldowns[ability.Id] = ability.ProcTimer;

            // Check happiness gating
            float happiness = _instance.Happiness;
            bool isVeryUnhappy = happiness < 0.2f;
            bool isHappy = happiness >= 0.5f;

            // Play proc effects
            // Only play sound immediately for non-walk abilities
            bool isWalkAbility = ability.AbilityClass is 
                "AnimalFriendship" or "NPCFriendship" or "FamiliarFriendship";

            if (!string.IsNullOrEmpty(ability.ProcSound) && !isWalkAbility)
                Game1.playSound(ability.ProcSound);

            if (ability.ProcAnimation)
                _entity.Sprite.Animate(
                    Game1.currentGameTime, 36, 4, 100f);

            // Execute ability class
            switch (ability.AbilityClass)
            {
                 case "Buff":
                    ExecuteBuff(ability, isHappy, isVeryUnhappy);
                    break;

                case "Heal":
                    if (isHappy)
                        ExecuteHeal(ability);
                    break;

                case "Energy":
                    if (isHappy)
                        ExecuteEnergy(ability);
                    break;

                case "Melee":
                ExecuteMelee(ability, location);
                break;

                case "ForageHarvest":
                    ExecuteForageHarvest(ability, location);
                    break;

                case "CropHarvest":
                    ExecuteCropHarvest(ability, location);
                    break;

                case "Fisher":
                    ExecuteFisher(ability, location);
                    break;

                case "AnimalFriendship":
                    ExecuteFriendship(ability, location, FriendshipTargetType.Animal);
                    break;

                case "NPCFriendship":
                    ExecuteFriendship(ability, location, FriendshipTargetType.NPC);
                    break;

                case "FamiliarFriendship":
                    ExecuteFriendship(ability, location, FriendshipTargetType.Familiar);
                    break;

                case "Nop":
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────
        // Walk System
        // ─────────────────────────────────────────────────────────

        private void StartAbilityWalk(Character target, System.Action onArrive)
        {
            _entity.AbilityTarget     = target;
            _entity.AbilityState      = FamiliarAbilityState.WalkingToTarget;
            _entity.OnArriveAtTarget  = onArrive;
        }

        private Character? FindNearestTarget(
            GameLocation location,
            int range,
            Func<Character, bool> filter)
        {
            // Search characters
            var charTarget = location.characters
                .Where(c => filter(c) && 
                    Vector2.Distance(c.Position, _entity.Position) <= range)
                .OrderBy(c => Vector2.Distance(c.Position, _entity.Position))
                .FirstOrDefault();

            return charTarget;
        }

        private FarmAnimal? FindNearestAnimal(GameLocation location, int range)
        {
            var allAnimals = new List<FarmAnimal>(location.animals.Values);
            foreach (var building in location.buildings)
            {
                var interior = building.GetIndoors();
                if (interior != null)
                    allAnimals.AddRange(interior.animals.Values);
            }

            return allAnimals
                .Where(a => Vector2.Distance(a.Position, _entity.Position) <= range)
                .OrderBy(a => Vector2.Distance(a.Position, _entity.Position))
                .FirstOrDefault();
        }

        // ─────────────────────────────────────────────────────────
        // Buff Abilities
        // ─────────────────────────────────────────────────────────

        private void ApplyBuff(AbilityData ability)
        {
            float happiness = _instance.Happiness;
            bool isVeryUnhappy = happiness < 0.2f;
            bool isHappy = happiness >= 0.5f;
            ExecuteBuff(ability, isHappy, isVeryUnhappy);
        }

        private void ExecuteBuff(AbilityData ability, bool isHappy, bool isVeryUnhappy)
        {
            string buffId = $"FAUNA_{_instance.FamiliarId}_{ability.Id}";
            RemoveBuff(buffId);

            if (!isHappy && !isVeryUnhappy) return;

            // Get buff parameters from Args
            string targetBuffId = ability.Args.GetValueOrDefault("BuffId", "luck");
            int magnitude = int.TryParse(
                ability.Args.GetValueOrDefault("Magnitude", "1"),
                out int mag) ? mag : 1;
            string durationStr = ability.Args.GetValueOrDefault("Duration", "");
            int duration;
            if (!int.TryParse(durationStr, out int dur) || dur == -1)
                duration = Buff.ENDLESS;
            else
                duration = dur;

            // Unhappy — flip magnitude
            if (isVeryUnhappy)
                magnitude = -Math.Abs(magnitude);

            // Build buff effects from the buff ID
            var effects = new BuffEffects();
            switch (targetBuffId)
            {
                case "luck":        effects.LuckLevel.Value      = magnitude; break;
                case "speed":       effects.Speed.Value          = magnitude; break;
                case "defense":     effects.Defense.Value        = magnitude; break;
                case "attack":      effects.Attack.Value         = magnitude; break;
                case "farming":     effects.FarmingLevel.Value   = magnitude; break;
                case "fishing":     effects.FishingLevel.Value   = magnitude; break;
                case "mining":      effects.MiningLevel.Value    = magnitude; break;
                case "foraging":    effects.ForagingLevel.Value  = magnitude; break;
                case "maxStamina":  effects.MaxStamina.Value     = magnitude; break;
                default:
                    // Unknown buff ID — log and skip
                    ModEntry.ModMonitor.Log(
                        $"[FAUNA] Unknown BuffId '{targetBuffId}' for ability {ability.Id}",
                        LogLevel.Warn);
                    return;
            }

            // Load icon
            Texture2D? icon = null;
            if (!string.IsNullOrEmpty(_data.Assistance?.BuffIcon))
            {
                try
                {
                    icon = ModEntry.ModHelper.GameContent.Load<Texture2D>(_data.Assistance.BuffIcon);
                }
                catch { }
            }

            var buff = new Buff(
                id:             buffId,
                source:         _instance.GetDisplayName(_data),
                displayName:    _instance.GetDisplayName(_data),
                duration:       duration,
                iconTexture:    icon,
                iconSheetIndex: 0,
                effects:        effects);

            Game1.player.applyBuff(buff);
            _activeBuffIds.Add(buffId);
        }

        private void RemoveBuff(string buffId)
        {
            Game1.player.buffs.Remove(buffId);
            _activeBuffIds.Remove(buffId);
        }

        private void RemoveAllBuffs()
        {
            foreach (var buffId in _activeBuffIds.ToList())
                Game1.player.buffs.Remove(buffId);
            _activeBuffIds.Clear();
        }

        // ─────────────────────────────────────────────────────────
        // Heal / Energy
        // ─────────────────────────────────────────────────────────

private void ExecuteHeal(AbilityData ability)
{
    int healAmount = int.TryParse(
        ability.Args.GetValueOrDefault("Amount", "10"),
        out int a) ? a : 10;

    Game1.player.health = Math.Min(
        Game1.player.health + healAmount,
        Game1.player.maxHealth);

    Game1.addHUDMessage(new HUDMessage(
        ModEntry.Translation.Get("Ability.Heal",
            new { amount = healAmount }).ToString(), 4));
}

        private void ExecuteEnergy(AbilityData ability)
{
    float amount = float.TryParse(
        ability.Args.GetValueOrDefault("Amount", "10"),
        out float a) ? a : 10f;

Game1.player.stamina = Math.Min(
    Game1.player.stamina + amount,
    (float)Game1.player.maxStamina.Value);

    Game1.addHUDMessage(new HUDMessage(
    ModEntry.Translation.Get("Ability.Energy",
        new { amount = (int)amount }).ToString(), 4));
}

        // ─────────────────────────────────────────────────────────
        // Friendship Abilities
        // ─────────────────────────────────────────────────────────

        private enum FriendshipTargetType { Animal, NPC, Familiar }

        private void ExecuteFriendship(
            AbilityData ability,
            GameLocation location,
            FriendshipTargetType targetType)
        {
            int range = int.TryParse(
                ability.Args.GetValueOrDefault("Range", "256"),
                out int r) ? r : 256;

            int amount = int.TryParse(
                ability.Args.GetValueOrDefault("Amount", "15"),
                out int a) ? a : 15;

            // Scale amount with happiness
            amount = (int)(amount * _instance.Happiness);
            if (amount <= 0) return;

            switch (targetType)
            {
                case FriendshipTargetType.Animal:
                    ApplyAnimalFriendship(location, range, amount);
                    break;
                case FriendshipTargetType.NPC:
                    ApplyNPCFriendship(location, range, amount);
                    break;
                case FriendshipTargetType.Familiar:
                    ApplyFamiliarFriendship(location, range, amount);
                    break;
            }
        }

       private void ApplyAnimalFriendship(
    GameLocation location, int range, int amount)
{
    var allAnimals = new List<FarmAnimal>(location.animals.Values);
    foreach (var building in location.buildings)
    {
        var interior = building.GetIndoors();
        if (interior != null)
            allAnimals.AddRange(interior.animals.Values);
    }

    var target = allAnimals
        .Where(a =>
            Vector2.Distance(a.Position, _entity.Position) <= range &&
            !_instance.FriendshipGivenToday.Contains($"Animal_{a.myID.Value}"))
        .OrderBy(a => Vector2.Distance(a.Position, _entity.Position))
        .FirstOrDefault();

    if (target == null) return;

    string targetKey = $"Animal_{target.myID.Value}";

    // Use walk system
    _entity.AbilityTarget    = target;
    _entity.AbilityState     = FamiliarAbilityState.WalkingToTarget;
    _entity.OnArriveAtTarget = () =>
    {
        if (_instance.FriendshipGivenToday.Contains(targetKey)) return;

        target.friendshipTowardFarmer.Value =
            (int)Math.Min(
                (float)target.friendshipTowardFarmer.Value + amount,
                1000f);

        _instance.FriendshipGivenToday.Add(targetKey);
        target.doEmote(20);
        _entity.doEmote(20);
        Game1.playSound("give_gift");
    };
}

private void ApplyNPCFriendship(
    GameLocation location, int range, int amount)
{
    var target = location.characters
        .OfType<NPC>()
        .Where(n => n is not FamiliarEntity
            && n.CanSocialize
            && Vector2.Distance(n.Position, _entity.Position) <= range
            && !_instance.FriendshipGivenToday.Contains($"NPC_{n.Name}"))
        .OrderBy(n => Vector2.Distance(n.Position, _entity.Position))
        .FirstOrDefault();

    if (target == null) return;

    string targetKey = $"NPC_{target.Name}";

    StartAbilityWalk(target, () =>
    {
        if (_instance.FriendshipGivenToday.Contains(targetKey)) return;

        if (Game1.player.friendshipData.TryGetValue(
            target.Name, out var friendship))
            friendship.Points = Math.Min(friendship.Points + amount, 2750);

        _instance.FriendshipGivenToday.Add(targetKey);
        target.doEmote(20);
        _entity.doEmote(20);
        Game1.playSound("give_gift");
    });
}

private void ApplyFamiliarFriendship(
    GameLocation location, int range, int amount)
{
    var target = location.characters
        .OfType<FamiliarEntity>()
        .Where(e => e.InstanceId != _entity.InstanceId
            && Vector2.Distance(e.Position, _entity.Position) <= range
            && !_instance.FriendshipGivenToday.Contains(
                $"Familiar_{e.InstanceId}"))
        .OrderBy(e => Vector2.Distance(e.Position, _entity.Position))
        .FirstOrDefault();

    if (target == null) return;

    string targetKey = $"Familiar_{target.InstanceId}";

    var targetInstance = ModEntry.FamiliarManager?.OwnedFamiliars
        .FirstOrDefault(f => f.InstanceId == target.InstanceId);

    if (targetInstance == null) return;

    StartAbilityWalk(target, () =>
    {
        if (_instance.FriendshipGivenToday.Contains(targetKey)) return;

        targetInstance.AddTrust(10);
        _instance.FriendshipGivenToday.Add(targetKey);
        target.doEmote(20);
        _entity.doEmote(20);
        Game1.playSound("give_gift");
    });
}

        private void ShowFriendshipEffect()
        {

            if (_entity.currentLocation == null) return;

            // Floating heart above familiar
            _entity.doEmote(20);
            // Also play a sound
            Game1.playSound("give_gift");
        }

        // ─────────────────────────────────────────────────────────
        // Melee
        // ─────────────────────────────────────────────────────────

        private float GetDamageMultiplier(int trustTier) => trustTier switch
        {
            0  => 1.0f,
            2  => 1.1f,
            4  => 1.2f,
            6  => 1.4f,
            8  => 1.7f,
            10 => 2.0f,
            _  => 1.0f
        };

        private void ExecuteMelee(AbilityData ability, GameLocation location)
        {
            if (_entity.AbilityState != FamiliarAbilityState.None) return;

            int range = int.TryParse(
                ability.Args.GetValueOrDefault("Range", "320"),
                out int r) ? r : 320;

            var target = location.characters
                .OfType<Monster>()
                .Where(m => Vector2.Distance(m.Position, _entity.Position) <= range)
                .OrderBy(m => Vector2.Distance(m.Position, _entity.Position))
                .FirstOrDefault();

            if (target == null) return;

            int baseDamage = int.TryParse(
                ability.Args.GetValueOrDefault("Damage", "5"),
                out int d) ? d : 5;

            float knockback = float.TryParse(
                ability.Args.GetValueOrDefault("Knockback", "4"),
                out float k) ? k : 4f;

            float multiplier = GetDamageMultiplier(_instance.TrustTier);
            int finalDamage = (int)(baseDamage * multiplier * _instance.Happiness);
            finalDamage = Math.Max(1, finalDamage); // always deal at least 1 damage

            StartAbilityWalk(target, () =>
            {
                // Switch to hitting state for animation
                _entity.AbilityState = FamiliarAbilityState.HittingTarget;
                _entity._meleeTarget = target;
                _entity._meleeDamage = finalDamage;
                _entity._meleeKnockback = knockback;
                _entity._meleeAnimTimer = 0;
            });
        }

        // ─────────────────────────────────────────────────────────
// Forage Harvest
// ─────────────────────────────────────────────────────────
private void ExecuteForageHarvest(AbilityData ability, GameLocation location)
{
    if (_entity.AbilityState != FamiliarAbilityState.None) return;
    if (_instance.Inventory.Count >= GetInventorySize()) return;

    int range = int.TryParse(
        ability.Args.GetValueOrDefault("Range", "320"),
        out int r) ? r : 320;

    // Find nearest forageable object
    var target = location.Objects.Values
        .Where(o => o.HasContextTag("forage_item") &&
            Vector2.Distance(o.TileLocation * 64f, _entity.Position) <= range)
        .OrderBy(o => Vector2.Distance(o.TileLocation * 64f, _entity.Position))
        .FirstOrDefault();

    if (target == null) return;

    // Store tile position since object might move
    Vector2 targetTile     = target.TileLocation;
    Vector2 targetPosition = targetTile * 64f;

    // Use a dummy character at the forage position for walk targeting
    // since StardewValley.Object doesn't extend Character
    _entity.AbilityTargetPosition = targetPosition;
    _entity.AbilityState          = FamiliarAbilityState.WalkingToPosition;
    _entity.OnArriveAtTarget      = () =>
    {
        // Check forage is still there
        if (!location.Objects.TryGetValue(targetTile, out var forage) ||
            !forage.HasContextTag("forage_item"))
            return;

        // Check inventory not full
        if (_instance.Inventory.Count >= GetInventorySize())
            return;

        // Add to familiar inventory
        _instance.Inventory.Add(forage.QualifiedItemId);

        // Remove from world
        location.Objects.Remove(targetTile);

        // Play proc animation
        if (!string.IsNullOrEmpty(ability.ProcSound))
            Game1.playSound(ability.ProcSound);

        // Floating text above familiar
        _entity.currentLocation?.temporarySprites.Add(
            new TemporaryAnimatedSprite
            {
                position      = new Vector2(_entity.Position.X, _entity.Position.Y - 64f),
                text          = "★",
                color         = Color.LightGreen,
                totalNumberOfLoops = 1,
                interval      = 1500f,
                layerDepth    = 1f,
                motion        = new Vector2(0f, -0.5f),
                alphaFade     = 0.005f
            });
    };
}

private void ExecuteCropHarvest(AbilityData ability, GameLocation location)
{
    if (_entity.AbilityState != FamiliarAbilityState.None) return;
    if (_instance.Inventory.Count >= GetInventorySize()) return;

    int range = int.TryParse(
        ability.Args.GetValueOrDefault("Range", "320"),
        out int r) ? r : 320;

    var target = location.terrainFeatures.Values
        .OfType<HoeDirt>()
        .Where(hd => hd.readyForHarvest() &&
            Vector2.Distance(hd.Tile * 64f, _entity.Position) <= range)
        .OrderBy(hd => Vector2.Distance(hd.Tile * 64f, _entity.Position))
        .FirstOrDefault();

    if (target == null) return;

    Vector2 targetPosition = target.Tile * 64f;

    _entity.AbilityTargetPosition = targetPosition;
    _entity.AbilityState          = FamiliarAbilityState.WalkingToPosition;
    _entity.OnArriveAtTarget = () =>
{
    if (!location.terrainFeatures.TryGetValue(target.Tile, out var tf) ||
        tf is not HoeDirt hd || !hd.readyForHarvest())
        return;

    if (_instance.Inventory.Count >= GetInventorySize()) return;
    if (hd.crop == null) return;

    string harvestItemId = hd.crop.indexOfHarvest.Value;
    if (!string.IsNullOrEmpty(harvestItemId))
    {
        _instance.Inventory.Add($"(O){harvestItemId}");
        hd.destroyCrop(false);

        // Sound and visual feedback
        Game1.playSound("harvest");
        _entity.currentLocation?.temporarySprites.Add(
            new TemporaryAnimatedSprite
            {
                position           = new Vector2(_entity.Position.X,
                                         _entity.Position.Y - 64f),
                text               = "★",
                color              = Color.LightGreen,
                totalNumberOfLoops = 1,
                interval           = 1500f,
                layerDepth         = 1f,
                motion             = new Vector2(0f, -0.5f),
                alphaFade          = 0.005f
            });
    }
};
}

// ─────────────────────────────────────────────────────────
// Fisher
// ─────────────────────────────────────────────────────────

private Vector2? FindNearestWaterTile(GameLocation location, Vector2 origin, int range)
{
    int originTileX = (int)(origin.X / 64f);
    int originTileY = (int)(origin.Y / 64f);
    int tileRange   = range / 64;

    Vector2? nearest = null;
    float nearestDist = float.MaxValue;

    int[] dx = { 0, 0, 1, -1 };
    int[] dy = { 1, -1, 0, 0 };

    for (int x = -tileRange; x <= tileRange; x++)
    {
        for (int y = -tileRange; y <= tileRange; y++)
        {
            int tx = originTileX + x;
            int ty = originTileY + y;

            if (tx < 0 || ty < 0 ||
                tx >= location.map.Layers[0].LayerSize.Width ||
                ty >= location.map.Layers[0].LayerSize.Height)
                continue;

            // Skip if not fishable water
            if (!location.isTileFishable(tx, ty)) continue;

            // Find an adjacent passable land tile to stand on
            for (int i = 0; i < 4; i++)
            {
                int landX = tx + dx[i];
                int landY = ty + dy[i];
                Vector2 landPos = new Vector2(landX * 64f, landY * 64f);

                // Must be passable (not water, not wall)
                if (_entity.IsTileBlocked(location, landPos)) continue;

                float dist = Vector2.Distance(origin, landPos);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = landPos;
                    // Store the actual water tile for bobberTile in ExecuteFisher
                    _nearestWaterTile = new Vector2(tx, ty); // tile coords, not pixel
                }
            }
        }
    }

    return nearest;
}

private void ExecuteFisher(AbilityData ability, GameLocation location)
{
    if (_entity.AbilityState != FamiliarAbilityState.None) return;
    if (_instance.Inventory.Count >= GetInventorySize()) return;

    int fisherRange = int.TryParse(
        ability.Args.GetValueOrDefault("Range", "384"),
        out int fisherR) ? fisherR : 384;

    Vector2? standingPos = FindNearestWaterTile(location, _entity.Position, fisherRange);
if (!standingPos.HasValue) return;

// Capture water tile before lambda
Vector2 bobberTile = _nearestWaterTile;

_entity.AbilityTargetPosition = standingPos.Value;
_entity.AbilityState          = FamiliarAbilityState.WalkingToPosition;
_entity.OnArriveAtTarget      = () =>
{
    if (_instance.Inventory.Count >= GetInventorySize()) return;

    Farmer who = Game1.player;
    int waterDepth = 4;

    Item? caught = location.getFish(
        millisecondsAfterNibble: 500f,
        bait: null,
        waterDepth: waterDepth,
        who: who,
        baitPotency: 0,
        bobberTile: bobberTile); // ← actual water tile coords

    if (caught == null) return;

    _instance.Inventory.Add(caught.QualifiedItemId);
    Game1.playSound("wateringCan");

    _entity.currentLocation?.temporarySprites.Add(
        new TemporaryAnimatedSprite
        {
            position           = new Vector2(_entity.Position.X,
                                     _entity.Position.Y - 64f),
            text               = "★",
            color              = Color.SkyBlue,
            totalNumberOfLoops = 1,
            interval           = 1500f,
            layerDepth         = 1f,
            motion             = new Vector2(0f, -0.5f),
            alphaFade          = 0.005f
        });
};
}

        // ─────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────

        private List<AbilityData>? GetCurrentAbilities()
        {
            if (_data.Assistance?.Abilities == null ||
                _data.Assistance.Abilities.Count == 0)
                return null;

            int level = _instance.GetAbilityLevel(_data);

            return _data.Assistance.Abilities[level];
        }

private bool HasNearbyTarget(
    AbilityData ability, GameLocation location)
{
    if (_entity.AbilityState != FamiliarAbilityState.None) return false;

    int range = int.TryParse(
        ability.Args.GetValueOrDefault("Range", "256"),
        out int r) ? r : 256;

    switch (ability.AbilityClass)
    {
        case "Melee":
            return location.characters
                .OfType<Monster>()
                .Any(m => Vector2.Distance(m.Position, _entity.Position) <= range);
                
        case "Heal":
            return Game1.player.health < Game1.player.maxHealth;

        case "Energy":
            return Game1.player.stamina < (float)Game1.player.maxStamina.Value;

        case "ForageHarvest":
        {
            if (_instance.Inventory.Count >= GetInventorySize()) return false;
            return location.Objects.Values.Any(o =>
                o.HasContextTag("forage_item") &&
                Vector2.Distance(o.TileLocation * 64f, _entity.Position) <= range);
        }
        case "CropHarvest":
        {
            if (_instance.Inventory.Count >= GetInventorySize()) return false;
            return location.terrainFeatures.Values
                .OfType<HoeDirt>()
                .Any(hd => hd.readyForHarvest() &&
                    Vector2.Distance(hd.Tile * 64f, _entity.Position) <= range);
        }
        case "Fisher":
        {
            if (_instance.Inventory.Count >= GetInventorySize()) return false;
            // Check if there's a fishable water tile nearby
            int fisherRange = int.TryParse(
                ability.Args.GetValueOrDefault("Range", "384"),
                out int fisherR) ? fisherR : 384;
            return FindNearestWaterTile(location, _entity.Position, range).HasValue;
        }
        case "AnimalFriendship":
        {
            var animals = new List<FarmAnimal>(location.animals.Values);
            foreach (var building in location.buildings)
            {
                var interior = building.GetIndoors();
                if (interior != null)
                    animals.AddRange(interior.animals.Values);
            }
            return animals.Any(a =>
                Vector2.Distance(a.Position, _entity.Position) <= range &&
                !_instance.FriendshipGivenToday.Contains(
                    $"Animal_{a.myID.Value}"));
        }

        case "NPCFriendship":
            return location.characters
                .OfType<NPC>()
                .Any(n => n is not FamiliarEntity
                    && n.CanSocialize
                    && Vector2.Distance(n.Position, _entity.Position) <= range
                    && !_instance.FriendshipGivenToday.Contains(
                        $"NPC_{n.Name}"));

        case "FamiliarFriendship":
            return location.characters
                .OfType<FamiliarEntity>()
                .Any(e => e.InstanceId != _entity.InstanceId
                    && Vector2.Distance(e.Position, _entity.Position) <= range
                    && !_instance.FriendshipGivenToday.Contains(
                        $"Familiar_{e.InstanceId}"));

        default:
            return false;
    }
}
private int GetInventorySize()
{
    return _data.Assistance?.InventorySize ?? 0;
}
    }
}