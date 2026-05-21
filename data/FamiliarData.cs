namespace FAUNA
{
    public class FamiliarData
    {
        // --- Identity ---
        // FamiliarId removed — dictionary key IS the ID now
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public string FamiliarType { get; set; } = "";
        public DietType Diet { get; set; } = DietType.Omnivore;

        // --- Default shop data ---
        public bool ExcludeFromDefaultShop { get; set; } = false;
        public int DefaultShopPrice { get; set; } = 5000;

        // --- Assets (game asset paths, not file paths) ---
        public string SpriteAsset { get; set; } = "";
        public string PortraitAsset { get; set; } = "";
        public string DialogueAsset { get; set; } = "";
        public string HumanoidSpriteAsset { get; set; } = "";
        public string HumanoidPortraitAsset { get; set; } = "";
        public string HumanoidDialogueAsset { get; set; } = "";
        public string RoomAsset { get; set; } = "";

        // --- Animal Form ---
        public bool Speaks { get; set; } = true;
        public bool AlwaysAnimate { get; set; } = false;

        // --- Humanoid Form (optional) ---
        public bool HasHumanoid { get; set; } = false;

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

    public class NeedsData
    {
        public float FoodDecayPerDay { get; set; } = 0.2f;
        public float AttentionDecayPerDay { get; set; } = 0.25f;
    }

    public class AbilityData
    {
        public string Id { get; set; } = "";
        public string AbilityClass { get; set; } = "Nop";
        public string Description { get; set; } = "";
        public string Proc { get; set; } = "OnFollow";
        public float ProcTimer { get; set; } = 60f;
        public string Condition { get; set; } = "";
        public string ProcSound { get; set; } = "";
        public bool ProcAnimation { get; set; } = false;
        public Dictionary<string, string> Args { get; set; } = new();
    }

    public class AssistanceData
    {
        public string BuffIcon { get; set; } = "";
        public int InventorySize { get; set; } = 0;
        public bool AllowDeposit { get; set; } = false;
        public List<List<AbilityData>> Abilities { get; set; } = new();
    }

    public class LootEntry
    {
        public string ItemId { get; set; } = "";
        public float Chance { get; set; } = 0.1f;
    }
}