namespace FAUNA
{
    public class FamiliarInstance
    {
        public string FamiliarId { get; set; } = "";
        public string InstanceId { get; set; } = Guid.NewGuid().ToString();
        // Add a custom name field — empty means use the species DisplayName
        public string CustomName { get; set; } = "";
        // Use this everywhere you want to show the familiar's name
        public string GetDisplayName(FamiliarData data) =>
            string.IsNullOrEmpty(CustomName) ? data.DisplayName : CustomName;

        public FamiliarForm CurrentForm { get; set; } = FamiliarForm.Animal;
        public FamiliarState CurrentState { get; set; } = FamiliarState.Passive;

        // Needs (0.0 = empty, 1.0 = fully satisfied)
        public float Food { get; set; } = 1.0f;
        public float Attention { get; set; } = 1.0f;

        // Computed — never stored
        public float Happiness => (Food + Attention) / 2f;

        // Trust points (0–2500, like NPC friendship)
        // Each heart = 250 points, max 10 hearts
        public int Trust { get; set; } = 0;

        private const int PointsPerHeart = 250;
        private const int MaxTrust = 2500; // 10 hearts

        public void AddTrust(int points)
        {
            Trust = Math.Clamp(Trust + points, 0, MaxTrust);
        }

        // 0-10 hearts for display
        public int TrustHearts => Trust / PointsPerHeart;

        // Trust tier for dialogue (0,2,4,6,8,10)
        public int TrustTier => (TrustHearts / 2) * 2;

        // Familiar's collected items — stored as qualified item IDs
        // Persists between sessions via our JSON save system
        public List<string> Inventory { get; set; } = new();

        // Current ability level based on trust tier
        public int GetAbilityLevel(FamiliarData data)
        {
            if (data.Assistance?.Abilities == null || 
                data.Assistance.Abilities.Count == 0)
                return 0;
                
            return Math.Min(TrustTier / 2, data.Assistance.Abilities.Count - 1);
        }

        // Mood for dialogue
        public string Mood => Happiness >= 0.5f ? "Happy" : "Sad";

        // Tracks which loved items have been gifted at least once
        // Stored as qualified item IDs e.g. "(O)395"
        public HashSet<string> RevealedLovedGifts { get; set; } = new();


        // Daily interaction flags (reset each morning)
        public bool FedToday { get; set; } = false;
        public bool PettedToday { get; set; } = false;
        public bool GiftedToday { get; set; } = false;
        public HashSet<string> SaidTodayLines { get; set; } = new();
        // Tracks which targets have received friendship today
        // Key format: "Animal_[animalId]", "NPC_[name]", "Familiar_[instanceId]"
        public HashSet<string> FriendshipGivenToday { get; set; } = new();
    }
    
}