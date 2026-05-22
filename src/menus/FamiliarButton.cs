using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace FAUNA
{
    /// <summary>
    /// A persistent HUD button that opens the Familiar Journal.
    /// Drawn in the top-right corner of the screen.
    /// </summary>
    public class FamiliarButton
    {
        // ─────────────────────────────────────────────────────────
        // Fields
        // ─────────────────────────────────────────────────────────

        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;

        // The clickable button region — recalculated on each draw
        private Rectangle _buttonBounds;

        public int _buttonYOffset = 0;

        // Bat icon loaded from game content
        private Texture2D? _buttonTexture;

        // Whether the button is currently visible
        private bool _visible = false;

        // ─────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────

        public FamiliarButton(IModHelper helper, IMonitor monitor)
        {
            _helper = helper;
            _monitor = monitor;
            _buttonYOffset = 
            helper.ModRegistry.IsLoaded("Annosz.UiInfoSuite2") ||
            helper.ModRegistry.IsLoaded("drewhoener.UIInfoSuiteAlternative")
                ? 54 : 0;

            helper.Events.Display.RenderingHud += OnRenderingHud;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        }

        // ─────────────────────────────────────────────────────────
        // Event handlers
        // ─────────────────────────────────────────────────────────

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            _visible = true;
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            _visible = false;
        }

        private void OnRenderingHud(object? sender, RenderingHudEventArgs e)
        {
            if (!_visible || !ShouldDraw())
                return;

            // Load the bat icon from game content on first draw
            // Load custom icon
if (_buttonTexture == null)
{
    _buttonTexture = _helper.ModContent.Load<Texture2D>("assets/icons/journal_button.png");
}
            int size = 44;
            int x = Game1.uiViewport.Width - 85;
            int y = 310 + _buttonYOffset;

            _buttonBounds = new Rectangle(x, y, size, size);

            // Draw button background
           IClickableMenu.drawTextureBox(
    e.SpriteBatch,
    Game1.menuTexture,
    new Rectangle(0, 256, 60, 60),
    x - 4,
    y - 4,
    size + 8,
    size + 8,
    Color.White,
    drawShadow: true
);
// Draw icon — no source rect needed since the whole texture is the icon
e.SpriteBatch.Draw(
    _buttonTexture,
    new Rectangle(x, y, size, size),
    Color.White
);

            // Tooltip on hover
            if (_buttonBounds.Contains(Game1.getMouseX(true), Game1.getMouseY(true)))
            {
                IClickableMenu.drawToolTip(
                    e.SpriteBatch,
                    "Familiar Journal",
                    "",
                    null
                );
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null)
                return;

            // Mouse click on button
            if (e.Button == SButton.MouseLeft &&
                _visible &&
                ShouldDraw() &&
                _buttonBounds.Contains(Game1.getMouseX(true), Game1.getMouseY(true)))
            {
                OpenJournal();
                return;
            }

            // Keybind
            if (ModEntry.Config.JournalKey.JustPressed())
            {
                OpenJournal();
            }
        }

        private void OpenJournal()
        {
            Game1.activeClickableMenu = new FamiliarMenu();
        }

        // ─────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────

        private bool ShouldDraw()
        {
            return Context.IsWorldReady &&
                   !Game1.eventUp &&
                   Game1.IsHudDrawn &&
                   ModEntry.Config.ShowButton;
        }
    }
}