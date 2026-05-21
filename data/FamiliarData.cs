namespace FAUNA
{


    // This is the definition of a Familiar TYPE - loaded from content pack JSON.
    // One FamiliarData exists per familiar species.
    public class FamiliarData
    {
        // --- Identity ---
        public string FamiliarId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public string FamiliarType { get; set; } = ""; // Combat, Foraging, Farming, Social, etc.
        public DietType Diet { get; set; } = DietType.Omnivore;


        // --- default shop data ---
        // If true, this familiar will NOT appear in the default Ouija Board shop
        public bool ExcludeFromDefaultShop { get; set; } = false;
        // Price when sold at the default Ouija Board shop
        // Only used if ExcludeFromDefaultShop is false
        public int DefaultShopPrice { get; set; } = 5000;

        // --- Animal Form ---
        public string AnimalSprite { get; set; } = "";
        public bool Speaks { get; set; } = true;
        public string AnimalPortrait { get; set; } = "";
        public string AnimalDialogue { get; set; } = "";
        // If true, familiar always animates even when idle (for flying familiars) (only in animal form)
        public bool AlwaysAnimate { get; set; } = false;

        // --- Humanoid Form (optional) ---
        public bool HasHumanoid { get; set; } = false;
        public string HumanoidSprite { get; set; } = "";
        public string HumanoidPortrait { get; set; } = "";
        public string HumanoidDialogue { get; set; } = "";

        // --- Needs ---
        public NeedsData Needs { get; set; } = new();

        // --- Assistance ---
        public AssistanceData Assistance { get; set; } = new();

        // --- Gift Preferences ---
        public List<string> LovedItems { get; set; } = new();
        public List<string> HatedItems { get; set; } = new();

        // --- Loot ---
        public bool DropsLoot { get; set; } = false;
        public List<LootEntry> LootPool { get; set; } = new();
    }

    // Defines how each need decays per day (0.0 to 1.0 scale)
    public class NeedsData
    {
        public float FoodDecayPerDay { get; set; } = 0.2f;
        public float AttentionDecayPerDay { get; set; } = 0.25f;
    }

    // Defines what the familiar does for the player, and how happiness scales it
/// <summary>
/// Defines a single ability a familiar can use.
/// </summary>
public class AbilityData
{
    // ── Identity ────────────────────────────────────────────
    // Unique ID for this ability — used for syncing and CP editing
    public string Id { get; set; } = "";

    // What the ability does
    // Standard: "LuckBuff", "SpeedBuff", "Heal", "Energy",
    //           "AnimalFriendship", "NPCFriendship", "FamiliarFriendship",
    //           "Melee", "Fisher", "CropHarvest",
    //           "AnimalHarvest", "ForageHarvest"
    public string AbilityClass { get; set; } = "Nop";

// Optional description shown in UI — use i18n key, not raw text!
// e.g. "BlackCat_LuckBuff_Description"
// Resolved via content pack's i18n/default.json
public string Description { get; set; } = "";

    // ── Proc ────────────────────────────────────────────────
    // When the ability activates
    // "OnFollow"   — timed, while following player
    // "OnNearby"   — when a valid target is within range
    public string Proc { get; set; } = "OnFollow";

    // Cooldown in real-time seconds between procs
    // -1 = no cooldown
    public float ProcTimer { get; set; } = 60f;

    // GSQ condition that must pass before proc
    // e.g. "PLAYER_HAS_BUFF farmer luck"
    public string Condition { get; set; } = "";

    // ── Proc Effects ────────────────────────────────────────
    // Sound cue to play on proc
    public string ProcSound { get; set; } = "";

    // If true, play Row 10 (frames 36-39) proc animation on proc
    public bool ProcAnimation { get; set; } = false;

    // ── Args ────────────────────────────────────────────────
    // Ability-class-specific arguments
    // Buff:               { "Magnitude": "1" }
    // Heal:               { "Amount": "10" } 
    // Energy:             { "Amount": "10" }
    // Friendship:         { "Range": "256", "Amount": "15" }
    // Melee:              { "Range": "128", "Damage": "5" }
    public Dictionary<string, string> Args { get; set; } = new();
}

/// <summary>
/// Defines the full ability set of a familiar, organized by trust level.
/// </summary>
public class AssistanceData
{
    // Buff bar icon path — shown while familiar is following
    // Falls back to a generic FAUNA icon if empty
    public string BuffIcon { get; set; } = "";

    // Inventory settings
    public int InventorySize { get; set; } = 0;      // 0 = no inventory
    public bool AllowDeposit { get; set; } = false;   // players can add items

    // Abilities organized by trust level.
    // Index 0 = Trust tier 0 (0 hearts)
    // Index 1 = Trust tier 2 (2 hearts)
    // Index 2 = Trust tier 4 (4 hearts)
    // Index 3 = Trust tier 6 (6 hearts)
    // Index 4 = Trust tier 8 (8 hearts)
    // Index 5 = Trust tier 10 (10 hearts)
    // Each level is a list of abilities that ALL run simultaneously
    public List<List<AbilityData>> Abilities { get; set; } = new();
}
    // One entry in a loot pool
    public class LootEntry
    {
        public string ItemId { get; set; } = "";
        public float Chance { get; set; } = 0.1f;
    }
}