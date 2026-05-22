using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewModdingAPI;
using StardewValley.Characters;
using StardewValley.Pathfinding;
using xTile.Dimensions;
using StardewValley.Buildings;
using StardewValley.Monsters;

namespace FAUNA
{
    public partial class FamiliarEntity : NPC
    {

        // ─────────────────────────────────────────────────────────
        // Identity
        // ─────────────────────────────────────────────────────────

        public string FamiliarId { get; private set; } = "";
        public string InstanceId { get; private set; } = "";

        // ─────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────

        public FamiliarForm CurrentForm { get; private set; } = FamiliarForm.Animal;
        public FamiliarState CurrentState { get; private set; } = FamiliarState.Passive;
        public int FollowIndex { get; set; } = 0;

        // ─────────────────────────────────────────────────────────
        // Textures
        // ─────────────────────────────────────────────────────────

        // Cached textures for each form — set at spawn time from content pack
        private Texture2D? _animalTexture;
        private Texture2D? _humanoidTexture;

        // ─────────────────────────────────────────────────────────
        // Movement / AI fields
        // ─────────────────────────────────────────────────────────

        // Idle chatter
        private int _idleChatterTimer    = 0;
        private int _idleChatterInterval = 0;
        private int _pendingResponseTimer     = 0;
        private FamiliarEntity?  _pendingResponder         = null;
        private FamiliarInstance? _pendingResponderInstance = null;

        private FamiliarAbilityHandler? _abilityHandler;
        public void TriggerDayStartAbilities()
        {
            _abilityHandler?.OnDayStart();
        }


        public FamiliarAbilityState AbilityState { get; set; } = FamiliarAbilityState.None;
        public Character? AbilityTarget { get; set; } = null;
        public System.Action? OnArriveAtTarget { get; set; } = null;

        public int _abilityWalkTimer = 0;
        private const int MaxAbilityWalkTicks = 300; // 5 seconds at 60fps
        public Vector2 AbilityTargetPosition { get; set; } = Vector2.Zero;

        // Melee hit state
        internal Monster? _meleeTarget     = null;
        internal int      _meleeDamage     = 0;
        internal float    _meleeKnockback  = 0f;
        internal int      _meleeAnimTimer  = 0;
        private const int MeleeAnimDuration = 40; // ~40 ticks to play 4 frames

        // ─────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Required parameterless constructor for save deserialization.
        /// Do not use directly — use SpawnFamiliars() in FamiliarManager instead.
        /// </summary>
        public FamiliarEntity() : base() { }

        /// <summary>
        /// Creates a FamiliarEntity from a data definition and instance record.
        /// spritePath is a temporary placeholder path — the real texture is loaded
        /// from the content pack and assigned immediately after construction.
        /// </summary>
        public FamiliarEntity(string familiarId, FamiliarData data, FamiliarInstance instance, Vector2 position, GameLocation location)
    : base(
        sprite: new AnimatedSprite(data.SpriteAsset, 0, 32, 32),
        position: position,
        facingDir: 2,
        name: familiarId.Replace(".", "_")
    )
{
    FamiliarId = familiarId;
    InstanceId = instance.InstanceId;
    displayName = instance.GetDisplayName(data);
    drawOffset = new Vector2(-32f, 0f);
    HideShadow = true;
    willDestroyObjectsUnderfoot = false;
    farmerPassesThrough = true;
    SimpleNonVillagerNPC = true;
    ignoreScheduleToday = true;
    returningToEndPoint = false;
    lastCrossroad = Microsoft.Xna.Framework.Rectangle.Empty;

    // Load sprite via GameContent
    try
    {
        Sprite.spriteTexture = ModEntry.ModHelper.GameContent.Load<Texture2D>(data.SpriteAsset);
        _animalTexture = Sprite.spriteTexture;
    }
    catch
    {
        ModEntry.ModMonitor.Log($"Could not load sprite for {familiarId}", LogLevel.Warn);
    }

    Sprite.sourceRect = new Microsoft.Xna.Framework.Rectangle(0, 0, 32, 32);
    Sprite.SpriteWidth = 32;
    Sprite.SpriteHeight = 32;

    // Load humanoid texture if applicable
    if (data.HasHumanoid && !string.IsNullOrEmpty(data.HumanoidSpriteAsset))
    {
        try
        {
            _humanoidTexture = ModEntry.ModHelper.GameContent.Load<Texture2D>(data.HumanoidSpriteAsset);
        }
        catch
        {
            ModEntry.ModMonitor.Log($"Could not load humanoid texture for {familiarId}", LogLevel.Warn);
        }
    }

    if (ModEntry.RegisteredFamiliars.TryGetValue(FamiliarId, out var abilityData))
        _abilityHandler = new FamiliarAbilityHandler(this, instance, abilityData);
}
            
        

        // ─────────────────────────────────────────────────────────
        // Core update loop
        // ─────────────────────────────────────────────────────────

        public override void update(GameTime time, GameLocation location)
        {
            currentLocation = location;

            // Handle jump physics
            if (yJumpOffset != 0)
            {
                yJumpVelocity += yJumpGravity;
                yJumpOffset -= (int)yJumpVelocity;
                if (yJumpOffset >= 0)
                {
                    yJumpOffset = 0;
                    yJumpVelocity = 0f;
                }
            }

            updateGlow();
            updateEmote(time);
            updateFaceTowardsFarmer(time, location);

            if (Game1.eventUp)
                return;

            // Cancel any NPCSchedule controller that snuck in
            if (controller != null && controller.NPCSchedule)
                controller = null;

            // Run familiar state behavior FIRST so controller is set correctly
            switch (CurrentState)
            {
                case FamiliarState.Passive:
                    UpdatePassive(time, location);
                    break;
                case FamiliarState.Following:
                    UpdateFollowing(time, location);
                    break;
            }

            
            if (Game1.IsMasterGame && !freezeMotion)
            {
                if (controller != null)
                {
                    if (controller.update(time))
                        controller = null;

                    if (xVelocity != 0f && yVelocity != 0f)
                    {
                        float mag = (float)Math.Sqrt(xVelocity * xVelocity + yVelocity * yVelocity);
                        float maxSpeed = speed * 4f;
                        if (mag > maxSpeed)
                        {
                            float scale = maxSpeed / mag;
                            xVelocity *= scale;
                            yVelocity *= scale;
                        }
                    }
                    position.UpdateExtrapolation((float)speed + addedSpeed);
                }
                else
                {
                    // Position is written directly in UpdatePassive/MoveToward
                    // Just clear velocity so nothing else interferes
                    xVelocity = 0f;
                    yVelocity = 0f;
                }
            }
        }

        // ─────────────────────────────────────────────────────────
        // State switching
        // ─────────────────────────────────────────────────────────

        public void SetState(FamiliarState newState)
{

    CurrentState = newState;
    controller = null;

    if (newState == FamiliarState.Following)
    {
        _abilityHandler?.OnStartFollowing();
    }
    else if (newState == FamiliarState.Passive)
    {
        speed = 2;
        _abilityHandler?.OnStopFollowing();
    }

    ModEntry.FamiliarManager?.UpdateFollowIndices();
}

        public void SwitchForm()
        {
            if (!ModEntry.RegisteredFamiliars.TryGetValue(FamiliarId, out var data))
                return;

            if (!data.HasHumanoid)
                return;

            CurrentForm = CurrentForm == FamiliarForm.Animal
                ? FamiliarForm.Humanoid
                : FamiliarForm.Animal;

            UpdateSprite(data);
        }

        
        // ─────────────────────────────────────────────────────────
        // Player interaction (right-click / action button)
        // ─────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────
// Interaction Entry Point
// ─────────────────────────────────────────────────────────
public override bool checkAction(Farmer who, GameLocation l)
{
    if (!Context.IsWorldReady) return false;

        // Check if the tile the player is facing has something to interact with
    // If so, let that take priority over us
    Vector2 facedTile = who.GetGrabTile();
    
    // Check for objects on the faced tile
    if (l.objects.ContainsKey(facedTile)) return false;
    
    // Check for terrain features (trees, crops, etc)
    if (l.terrainFeatures.ContainsKey(facedTile)) return false;
    
    // Check for resource clumps (boulders, logs)
    if (l.resourceClumps.Any(rc => rc.occupiesTile((int)facedTile.X, (int)facedTile.Y)))
        return false;

    var instance = ModEntry.FamiliarManager?.OwnedFamiliars
        .FirstOrDefault(f => f.InstanceId == InstanceId);
    if (instance == null) return false;

    if (!ModEntry.RegisteredFamiliars.TryGetValue(FamiliarId, out var data))
        return false;

    Item? heldItem = who.CurrentItem;

    // Holding food → feed
    if (heldItem != null && FamiliarManager.IsAcceptableFood(heldItem, data.Diet))
    {
        TryFeed(who, instance, data);
        return true;
    }

    // Holding a giftable item (not a tool/weapon/equipment) → gift
    if (heldItem != null && IsGiftableItem(heldItem))
    {
        TryGift(who, instance, data, heldItem);
        return true;
    }

    // Holding nothing (or non-giftable) → interaction menu
    OpenInteractionMenu(who, instance, data);
    return true;
}

// ─────────────────────────────────────────────────────────
// Feed
// ─────────────────────────────────────────────────────────
private void TryFeed(Farmer who, FamiliarInstance instance, FamiliarData data)
{
    if (instance.FedToday)
    {
        ShowDialogue(instance, data, "AlreadyFed",
            ModEntry.Translation.Get("Dialogue.AlreadyFed", 
    new { name = instance.GetDisplayName(data) }));
        return;
    }

    instance.Food = Math.Min(1f, instance.Food + 0.25f);
    instance.FedToday = true;

    string itemName = who.CurrentItem.Name;
    who.CurrentItem.Stack--;
    if (who.CurrentItem.Stack <= 0)
        who.removeItemFromInventory(who.CurrentItem);

    Game1.playSound("eat");
    showTextAboveHead("★", duration: 1000);

    ShowDialogue(instance, data, "Fed",
        ModEntry.Translation.Get("Dialogue.Fed",
    new { name = instance.GetDisplayName(data), item = itemName }));
}

// ─────────────────────────────────────────────────────────
// Gift
// ─────────────────────────────────────────────────────────
private bool IsGiftableItem(Item item)
{
    if (item is StardewValley.Tool) return false;
    if (item is StardewValley.Objects.Boots) return false;
    if (item is StardewValley.Objects.Ring) return false;
    if (item is StardewValley.Objects.Hat) return false;
    if (item.Category == StardewValley.Object.weaponCategory) return false;
    if (item.Category == StardewValley.Object.furnitureCategory) return false;
    if (item.Category == StardewValley.Object.skillBooksCategory) return false;
    return true;
}

private void TryGift(Farmer who, FamiliarInstance instance, FamiliarData data, Item gift)
{
    if (instance.GiftedToday)
    {
        ShowDialogue(instance, data, "AlreadyGifted",
            ModEntry.Translation.Get("Dialogue.AlreadyGifted",
    new { name = instance.GetDisplayName(data) }));
        return;
    }

    bool isLoved = data.LovedItems?.Contains(gift.QualifiedItemId) == true;
    bool isHated = data.HatedItems?.Contains(gift.QualifiedItemId) == true;

    string dialogueKey;
    string fallback;
    int    trustChange;
    string name = instance.GetDisplayName(data);


    if (isLoved)
    {
        dialogueKey  = "Gift_Loved";
        fallback     = ModEntry.Translation.Get("Dialogue.Gift.Loved", new { name });
        trustChange  = 50;
        Game1.playSound("give_gift");
        showTextAboveHead("♥", duration: 1500);
         // Reveal this gift in the log panel
        instance.RevealedLovedGifts.Add(gift.QualifiedItemId);
        instance.Attention = Math.Min(1f, instance.Attention + 0.25f);
    }
    else if (isHated)
    {
        dialogueKey  = "Gift_Hated";
        fallback     = ModEntry.Translation.Get("Dialogue.Gift.Hated", new { name });
        trustChange  = -20;
        Game1.playSound("cancel");
        showTextAboveHead("✖", duration: 1500);
    }
    else
    {
        dialogueKey  = "Gift_Neutral";
        fallback     = ModEntry.Translation.Get("Dialogue.Gift.Neutral", new { name });
        trustChange  = 15;
        Game1.playSound("give_gift");
        showTextAboveHead("★", duration: 1000);
        instance.Attention = Math.Min(1f, instance.Attention + 0.10f);
    }

    instance.AddTrust(trustChange);
    instance.GiftedToday = true;

    who.CurrentItem.Stack--;
    if (who.CurrentItem.Stack <= 0)
        who.removeItemFromInventory(who.CurrentItem);

    ShowDialogue(instance, data, dialogueKey, fallback);
}

// ─────────────────────────────────────────────────────────
// Interaction Menu
// ─────────────────────────────────────────────────────────
private void OpenInteractionMenu(Farmer who, FamiliarInstance instance, FamiliarData data)
{
    Game1.activeClickableMenu = new FamiliarInteractionMenu(who, instance, data, this);
}

// ─────────────────────────────────────────────────────────
// Chat
// ─────────────────────────────────────────────────────────
public void HandleChat(Farmer who, FamiliarInstance instance, FamiliarData data)
{
    if (instance.PettedToday)
    {
        ShowDialogue(instance, data, "AlreadyChatted",
            ModEntry.Translation.Get("Dialogue.AlreadyChatted",
    new { name = instance.GetDisplayName(data) }));
        return;
    }

    instance.PettedToday = true;
    instance.Attention = Math.Min(1f, instance.Attention + 0.25f);
    instance.AddTrust(20);

    ShowDialogue(instance, data, "Chat", GetFallbackChatDialogue(instance, data));
}

// ─────────────────────────────────────────────────────────
// Dialogue Helpers
// ─────────────────────────────────────────────────────────

// Main dialogue display — tries i18n key first, uses fallback if not found
private void ShowDialogue(FamiliarInstance instance, FamiliarData data,
    string key, string fallback)
{
    string line = GetDialogueLine(instance, key) ?? fallback;

    CurrentDialogue.Clear();
    CurrentDialogue.Push(new Dialogue(this, "action", line));
    Game1.drawDialogue(this);
}

// Tries to find a dialogue line in the content pack's i18n
private string? GetDialogueLine(FamiliarInstance instance, string baseKey)
{
    if (!ModEntry.RegisteredFamiliars.TryGetValue(FamiliarId, out var familiarData))
        return null;

    // Pick dialogue asset based on current form
    string dialogueAsset = instance.CurrentForm == FamiliarForm.Animal
        ? familiarData.DialogueAsset
        : familiarData.HumanoidDialogueAsset;

    if (string.IsNullOrEmpty(dialogueAsset)) return null;

    var dialogueDict = FamiliarCache.GetDialogue(FamiliarId);
    if (dialogueDict == null) return null;

    string[] keysToTry =
    {
        $"{baseKey}_Trust{instance.TrustTier}_{instance.Mood}",
        baseKey
    };

    foreach (string key in keysToTry)
    {
        var lines = new List<string>();
        int index = 0;

        while (true)
        {
            if (!dialogueDict.TryGetValue($"{key}_{index}", out string? line))
                break;
            lines.Add(line); // no token resolution needed — CP already handled it
            index++;
        }

        if (lines.Count > 0)
            return lines[Game1.random.Next(lines.Count)];
    }

    return null;
}


// Fallback chat dialogue based on trust tier and mood
private string GetFallbackChatDialogue(FamiliarInstance instance, FamiliarData data)
{
    string name = instance.GetDisplayName(data);
    string key = (instance.TrustTier, instance.Mood) switch
    {
        (0,  "Happy") => "Dialogue.Chat.Trust0.Happy.0",
        (0,  "Sad")   => "Dialogue.Chat.Trust0.Sad.0",
        (2,  "Happy") => "Dialogue.Chat.Trust2.Happy.0",
        (2,  "Sad")   => "Dialogue.Chat.Trust2.Sad.0",
        (4,  "Happy") => "Dialogue.Chat.Trust4.Happy.0",
        (4,  "Sad")   => "Dialogue.Chat.Trust4.Sad.0",
        (6,  "Happy") => "Dialogue.Chat.Trust6.Happy.0",
        (6,  "Sad")   => "Dialogue.Chat.Trust6.Sad.0",
        (8,  "Happy") => "Dialogue.Chat.Trust8.Happy.0",
        (8,  "Sad")   => "Dialogue.Chat.Trust8.Sad.0",
        (10, "Happy") => "Dialogue.Chat.Trust10.Happy.0",
        (10, "Sad")   => "Dialogue.Chat.Trust10.Sad.0",
        _             => "Dialogue.Chat.Default"
    };
    return ModEntry.Translation.Get(key, new { name }).ToString();
}



        // ─────────────────────────────────────────────────────────
        // NPC overrides — replacing vanilla NPC behavior
        // ─────────────────────────────────────────────────────────

        public override void dayUpdate(int dayOfMonth)
        {
            // Do NOT call base.dayUpdate() — we don't want pet bowl,
            // farmhouse warping, or any other NPC day logic
            SetState(FamiliarState.Passive);
            Sprite.loop = true;
        }

        public override void checkSchedule(int timeOfDay)
        {
            // Familiars don't have NPC schedules — intentionally empty
        }

        public override void performTenMinuteUpdate(int timeOfDay, GameLocation location)
        {
            // Familiars don't do NPC ten-minute updates — intentionally empty
        }

        public override void resetForNewDay(int dayOfMonth)
        {
            // Let base handle standard reset (clears controllers, resets position etc)
            base.resetForNewDay(dayOfMonth);

            // Re-suppress everything the base just reset —
            // resetForNewDay sets ignoreScheduleToday = false and may load a schedule
            ignoreScheduleToday = true;
            returningToEndPoint = false;
            lastCrossroad = Microsoft.Xna.Framework.Rectangle.Empty;
            controller = null;
            temporaryController = null;
            queuedSchedulePaths.Clear();
        }

        // ─────────────────────────────────────────────────────────
        // Sprite helpers
        // ─────────────────────────────────────────────────────────

        private void UpdateSprite(FamiliarData data)
        {
            string spriteAsset = CurrentForm == FamiliarForm.Animal
                ? data.SpriteAsset
                : data.HumanoidSpriteAsset;

            int spriteWidth = CurrentForm == FamiliarForm.Animal ? 32 : 16;

            try
            {
                var texture = ModEntry.ModHelper.GameContent.Load<Texture2D>(spriteAsset);
                Sprite.spriteTexture = texture;
                Sprite.SpriteWidth = spriteWidth;
                Sprite.SpriteHeight = 32;
            }
            catch
            {
                ModEntry.ModMonitor.Log($"Could not load sprite {spriteAsset} for {FamiliarId}", LogLevel.Warn);
            }
        }

        public override Microsoft.Xna.Framework.Rectangle GetBoundingBox()
        {
            return new Microsoft.Xna.Framework.Rectangle(
                (int)Position.X + 8,
                (int)Position.Y + 16,
                16,
                16);
        }

       // ─────────────────────────────────────────────────────────
        // Idle Chatter
        // ─────────────────────────────────────────────────────────

        private void UpdateIdleChatter(FamiliarInstance instance, GameLocation location)
        {
            // Only when passive and player is in same location
            if (CurrentState != FamiliarState.Passive) return;
            if (Game1.player.currentLocation != location) return;

            // Initialize interval on first run
            if (_idleChatterInterval == 0)
                _idleChatterInterval = Game1.random.Next(1800, 3001);

            // Handle pending response from another familiar's chatter
            if (_pendingResponseTimer > 0)
            {
                _pendingResponseTimer--;
                if (_pendingResponseTimer == 0 && 
                    _pendingResponder != null && 
                    _pendingResponderInstance != null)
                {
                    string? responseLine = GetUnsaidDialogueLine(
                        _pendingResponderInstance, "IdleResponse");
                    if (responseLine != null)
                    {
                        _pendingResponderInstance.SaidTodayLines.Add(
                            $"IdleResponse_{responseLine}");
                        _pendingResponder.showTextAboveHead(responseLine, duration: 3000);
                    }
                    _pendingResponder         = null;
                    _pendingResponderInstance = null;
                }
                return;
            }

            // Count up to interval
            _idleChatterTimer++;

            if (_idleChatterTimer < _idleChatterInterval) return;

            // Reset timer with new random interval
            _idleChatterTimer    = 0;
            _idleChatterInterval = Game1.random.Next(1800, 3001);

            // Try to say an idle line
            string? line = GetUnsaidDialogueLine(instance, "Idle");
            if (line == null) return;

            instance.SaidTodayLines.Add(line);

if (currentLocation != null)
{
    currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite
    {
        position = new Vector2(Position.X, Position.Y - 64f),
        text = line,
        color = Microsoft.Xna.Framework.Color.White,
        totalNumberOfLoops = 1,
        interval = 3000f,
        layerDepth = 1f,
        motion = new Vector2(0f, -0.5f),
        alphaFade = 0.003f
    });
}

            // 50% chance a nearby passive familiar responds after 2 seconds
            if (Game1.random.NextDouble() > 0.5) return;

            var responder = location.characters
                .OfType<FamiliarEntity>()
                .FirstOrDefault(e =>
                    e.InstanceId != InstanceId &&
                    e.CurrentState == FamiliarState.Passive &&
                    Vector2.Distance(e.Position, Position) <= 128f);

            if (responder == null) return;

            var responderInstance = ModEntry.FamiliarManager?.OwnedFamiliars
                .FirstOrDefault(f => f.InstanceId == responder.InstanceId);

            if (responderInstance == null) return;

            // Tell the responder to reply after 120 ticks (2 seconds)
            responder._pendingResponseTimer    = 120;
            responder._pendingResponder        = responder;
            responder._pendingResponderInstance = responderInstance;
        }

        // Gets a random unsaid line for a given key prefix (e.g. "Idle", "IdleResponse")
        private string? GetUnsaidDialogueLine(FamiliarInstance instance, string keyPrefix)
{
    if (!ModEntry.RegisteredFamiliars.TryGetValue(instance.FamiliarId, out var data))
        return null;

    string dialogueAsset = instance.CurrentForm == FamiliarForm.Animal
        ? data.DialogueAsset
        : data.HumanoidDialogueAsset;

    if (string.IsNullOrEmpty(dialogueAsset)) return null;

    var dialogueDict = FamiliarCache.GetDialogue(instance.FamiliarId);
    if (dialogueDict == null) return null;

    var available = new List<string>();
    int index = 0;
    while (true)
    {
        string key = $"{keyPrefix}_{index}";
        if (!dialogueDict.TryGetValue(key, out string? line)) break;
        if (!instance.SaidTodayLines.Contains(key))
            available.Add(line); // CP already resolved tokens
        index++;
    }

    return available.Count > 0 ? available[Game1.random.Next(available.Count)] : null;
}

        private void UpdateHittingTarget(GameTime time, GameLocation location)
        {
            _meleeAnimTimer++;

            Sprite.Animate(time, 36, 4, 100f);

            if (_meleeAnimTimer == MeleeAnimDuration / 2)
            {
                if (_meleeTarget != null && 
                    location.characters.Contains(_meleeTarget) &&
                    _meleeTarget.invincibleCountdown <= 0)
                    
                {
                    faceGeneralDirection(_meleeTarget.Position);

                    Vector2 knockbackDir = _meleeTarget.Position - Position;
                    if (knockbackDir != Vector2.Zero)
                        knockbackDir.Normalize();

                   int healthBefore = _meleeTarget.Health;

                    int remainingHealth = _meleeTarget.takeDamage(
                        _meleeDamage,
                        (int)(knockbackDir.X * _meleeKnockback),
                        (int)(knockbackDir.Y * _meleeKnockback),
                        false,
                        0,
                        Game1.player);

                    location.debris.Add(new Debris(
                        _meleeDamage,
                        new Vector2(_meleeTarget.Position.X, 
                            _meleeTarget.Position.Y - 32f),
                        Color.Red,
                        1f,
                        _meleeTarget));

                    // Use actual health property, not return value
                    if (_meleeTarget.Health <= 0 || 
                        !location.characters.Contains(_meleeTarget))
                    {
                        _meleeTarget    = null;
                        AbilityState    = FamiliarAbilityState.None;
                        _meleeAnimTimer = 0;
                        _abilityHandler?.OnAbilityWalkCompleted();
                        return;
                    }
                }
            }

            if (_meleeAnimTimer >= MeleeAnimDuration)
            {
                // If monster still alive and nearby, hit again
                if (_meleeTarget != null && 
                    location.characters.Contains(_meleeTarget) &&
                    Vector2.Distance(Position, _meleeTarget.Position) <= 96f)
                {
                    _meleeAnimTimer = 0; // reset and hit again
                }
                else
                {
                    _meleeTarget    = null;
                    _meleeDamage    = 0;
                    _meleeKnockback = 0f;
                    _meleeAnimTimer = 0;
                    AbilityState    = FamiliarAbilityState.ReturningToPlayer;
                }
            }
        }

public void UpdateAbilityMovement(GameTime time, GameLocation location, Farmer player)
{
    float spd = player.getMovementSpeed();
    Vector2 followTarget = GetFollowTarget(player); // needed for WalkingToTarget and ReturningToPlayer

    switch (AbilityState)
    {
        case FamiliarAbilityState.WalkingToTarget:
        {
            _abilityWalkTimer++;
            if (_abilityWalkTimer >= MaxAbilityWalkTicks)
            {
                _abilityWalkTimer = 0;
                OnArriveAtTarget = null;
                AbilityTarget = null;
                AbilityState = FamiliarAbilityState.ReturningToPlayer;
                return;
            }

            if (AbilityTarget == null || AbilityTarget.currentLocation != location)
            {
                AbilityState = FamiliarAbilityState.ReturningToPlayer;
                AbilityTarget = null;
                return;
            }

            float distToTarget = Vector2.Distance(Position, AbilityTarget.Position);
            if (distToTarget <= 96f)
            {
                OnArriveAtTarget?.Invoke();
                OnArriveAtTarget = null;
                AbilityTarget = null;
                if (AbilityState == FamiliarAbilityState.WalkingToTarget)
                    AbilityState = FamiliarAbilityState.ReturningToPlayer;
                return;
            }

            MoveToward(time, location, AbilityTarget.Position, spd);
            break;
        }

        case FamiliarAbilityState.WalkingToPosition:
        {
            _abilityWalkTimer++;
            if (_abilityWalkTimer >= MaxAbilityWalkTicks)
            {
                _abilityWalkTimer = 0;
                OnArriveAtTarget = null;
                AbilityTargetPosition = Vector2.Zero;
                AbilityState = FamiliarAbilityState.ReturningToPlayer;
                return;
            }

            float distToPos = Vector2.Distance(Position, AbilityTargetPosition);
            if (distToPos <= 128f)
            {
                OnArriveAtTarget?.Invoke();
                OnArriveAtTarget = null;
                AbilityTargetPosition = Vector2.Zero;
                if (AbilityState == FamiliarAbilityState.WalkingToPosition)
                    AbilityState = FamiliarAbilityState.ReturningToPlayer;
                return;
            }

            float moveAmount = Math.Min(spd, distToPos);
            MoveToward(time, location, AbilityTargetPosition, moveAmount);
            break;
        }

        case FamiliarAbilityState.ReturningToPlayer:
        {
            float distToPlayer = Vector2.Distance(Position, player.Position);
            float returnStopDistance = FollowStopDistance + (FollowIndex * 64f) + 32f;

            if (distToPlayer <= returnStopDistance)
            {
                AbilityState = FamiliarAbilityState.None;
                _abilityHandler?.OnAbilityWalkCompleted();
                return;
            }

            float moveAmount = Math.Min(spd, distToPlayer);
            MoveToward(time, location, followTarget, moveAmount);
            break;
        }

        case FamiliarAbilityState.HittingTarget:
            UpdateHittingTarget(time, location);
            break;
    }
}
    }
}