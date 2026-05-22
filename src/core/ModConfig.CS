using StardewModdingAPI.Utilities;

namespace FAUNA
{
    public class ModConfig
    {
        // Whether the HUD button is visible
        public bool ShowButton { get; set; } = true;

        // Keybind to open the Familiar Journal
        public KeybindList JournalKey { get; set; } = 
            KeybindList.Parse("F");
    }
}