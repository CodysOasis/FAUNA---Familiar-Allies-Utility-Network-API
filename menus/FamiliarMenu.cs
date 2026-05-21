using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using StardewValley.BellsAndWhistles;

namespace FAUNA
{
    public class FamiliarMenu : IClickableMenu
    {
        // ─────────────────────────────────────────────────────────
        // Layout
        // ─────────────────────────────────────────────────────────

        private const int MenuWidth  = 800;
        private const int MenuHeight = 520;

        // ─────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────

        private enum Screen { List, Detail }
        private Screen _currentScreen = Screen.List;

        // Which familiar is selected in the detail view
        private int _selectedIndex = 0;

        // Scrolling for the list view
        private int _scrollOffset = 0;
        private const int RowHeight = 96;
        private const int VisibleRows = 5;

        // Clickable areas (rebuilt each draw)
        private List<Rectangle> _rowRects = new();
        private Rectangle _backButtonRect;
        private Rectangle _leftArrowRect;
        private Rectangle _rightArrowRect;

        // ─────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────

        public FamiliarMenu()
            : base(
                x:      Game1.uiViewport.Width  / 2 - MenuWidth  / 2,
                y:      Game1.uiViewport.Height / 2 - MenuHeight / 2,
                width:  MenuWidth,
                height: MenuHeight,
                showUpperRightCloseButton: true
            )
        { }

        // ─────────────────────────────────────────────────────────
        // Input
        // ─────────────────────────────────────────────────────────

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_currentScreen == Screen.List)
            {
                for (int i = 0; i < _rowRects.Count; i++)
                {
                    if (_rowRects[i].Contains(x, y))
                    {
                        _selectedIndex = _scrollOffset + i;
                        _currentScreen = Screen.Detail;
                        Game1.playSound("smallSelect");
                        return;
                    }
                }
            }
            else // Detail
            {
                if (_backButtonRect.Contains(x, y))
                {
                    _currentScreen = Screen.List;
                    Game1.playSound("smallSelect");
                    return;
                }

                var familiars = ModEntry.FamiliarManager?.OwnedFamiliars;
                if (familiars != null)
                {
                    if (_leftArrowRect.Contains(x, y))
                    {
                        _selectedIndex = (_selectedIndex - 1 + familiars.Count) % familiars.Count;
                        Game1.playSound("shwip");
                        return;
                    }
                    if (_rightArrowRect.Contains(x, y))
                    {
                        _selectedIndex = (_selectedIndex + 1) % familiars.Count;
                        Game1.playSound("shwip");
                        return;
                    }
                }
            }
        }

        public override void receiveScrollWheelAction(int direction)
        {
            if (_currentScreen != Screen.List) return;
            int total = ModEntry.FamiliarManager?.OwnedFamiliars.Count ?? 0;
            int maxScroll = Math.Max(0, total - VisibleRows);
            _scrollOffset = Math.Clamp(_scrollOffset - Math.Sign(direction), 0, maxScroll);
        }

        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        // ─────────────────────────────────────────────────────────
        // Draw
        // ─────────────────────────────────────────────────────────

        public override void draw(SpriteBatch b)
        {
            // Background dim
            b.Draw(Game1.fadeToBlackRect,
                Game1.graphics.GraphicsDevice.Viewport.Bounds,
                Color.Black * 0.5f);

            // Menu box
            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                xPositionOnScreen, yPositionOnScreen,
                width, height, Color.White);

            // Title
            SpriteText.drawStringWithScrollCenteredAt(b, "Familiar Log",
                xPositionOnScreen + width / 2,
                yPositionOnScreen + 32);

            if (_currentScreen == Screen.List)
                DrawListScreen(b);
            else
                DrawDetailScreen(b);

            base.draw(b);
            drawMouse(b);
        }

        // ─────────────────────────────────────────────────────────
        // Screen 1 — List
        // ─────────────────────────────────────────────────────────

        private void DrawListScreen(SpriteBatch b)
        {
            _rowRects.Clear();

            var familiars = ModEntry.FamiliarManager?.OwnedFamiliars;

            if (familiars == null || familiars.Count == 0)
            {
                Utility.drawTextWithShadow(b, ModEntry.Translation.Get("UI.Log.NoFamiliars"),
                    Game1.dialogueFont,
                    new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 120),
                    Game1.textColor);
                return;
            }

            int listTop = yPositionOnScreen + 90;

            for (int i = 0; i < VisibleRows; i++)
            {
                int dataIndex = _scrollOffset + i;
                if (dataIndex >= familiars.Count) break;

                var instance = familiars[dataIndex];
                if (!ModEntry.RegisteredFamiliars.TryGetValue(instance.FamiliarId, out var data))
                    continue;

                int rowY = listTop + i * RowHeight;
                var rowRect = new Rectangle(xPositionOnScreen + 20, rowY, width - 40, RowHeight - 4);
                _rowRects.Add(rowRect);

                // Row background (highlight on hover)
                bool hovered = rowRect.Contains(Game1.getMouseX(), Game1.getMouseY());
                IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height,
                    hovered ? Color.Wheat : Color.White, drawShadow: false);

               if (ModEntry.FamiliarTextures.TryGetValue(instance.FamiliarId, out var tex))
{
    // Draw the sit-front frame (row 5, frame 0) — a natural "portrait" pose
    // Row 5 = y offset 128 (4 rows * 32px), first frame x = 0
    b.Draw(tex,
        new Rectangle(rowRect.X + 8, rowRect.Y + 4, 72, 72),
        new Rectangle(0, 128, 32, 32),
        Color.White);
}
else
{
    b.Draw(Game1.fadeToBlackRect,
        new Rectangle(rowRect.X + 8, rowRect.Y + 8, 48, 48),
        Color.DimGray * 0.6f);
}
                // Name
                Utility.drawTextWithShadow(b, instance.GetDisplayName(data),
                    Game1.dialogueFont,
                    new Vector2(rowRect.X + 68, rowRect.Y + 24),
                    Game1.textColor);

               int iconX = rowRect.Right - 160;
int iconY = rowRect.Y + 24;

// Food icon + check
b.Draw(Game1.mouseCursors, new Vector2(iconX - 7, iconY - 5),
    new Rectangle(10, 428, 9, 9),
    Color.White, 0f, Vector2.Zero, 3.5f, SpriteEffects.None, 0.9f);
DrawCheckIcon(b, instance.FedToday, iconX, iconY + 10);

// Gift icon + check
b.Draw(Game1.mouseCursors, new Vector2(iconX + 40, iconY - 13),
    new Rectangle(227, 408, 16, 16),
    Color.White, 0f, Vector2.Zero, 2.5f, SpriteEffects.None, 0.9f);
DrawCheckIcon(b, instance.GiftedToday, iconX + 52, iconY + 10);

// Pet/hand icon + check
b.Draw(Game1.mouseCursors, new Vector2(iconX + 95, iconY - 5),
    new Rectangle(32, 0, 9, 9),
    Color.White, 0f, Vector2.Zero, 3.5f, SpriteEffects.None, 0.9f);
DrawCheckIcon(b, instance.PettedToday, iconX + 104, iconY + 10);
            }
        }

        // ─────────────────────────────────────────────────────────
        // Screen 2 — Detail
        // ─────────────────────────────────────────────────────────

        private void DrawDetailScreen(SpriteBatch b)
{
    var familiars = ModEntry.FamiliarManager?.OwnedFamiliars;
    if (familiars == null || familiars.Count == 0) return;

    _selectedIndex = Math.Clamp(_selectedIndex, 0, familiars.Count - 1);
    var instance = familiars[_selectedIndex];
    if (!ModEntry.RegisteredFamiliars.TryGetValue(instance.FamiliarId, out var data))
        return;

    int contentTop = yPositionOnScreen + 90;

    // ── Back button ──────────────────────────────────────
    _backButtonRect = new Rectangle(xPositionOnScreen + 16, yPositionOnScreen + 8, 64, 44);
    bool backHovered = _backButtonRect.Contains(Game1.getMouseX(), Game1.getMouseY());
    Utility.drawTextWithShadow(b, ModEntry.Translation.Get("UI.Log.Back"),
        Game1.smallFont,
        new Vector2(_backButtonRect.X, _backButtonRect.Y + 12),
        backHovered ? Color.SaddleBrown : Game1.textColor);

    // ── Left panel: portrait + arrows + name ─────────────
    int portraitSize = 120;
    int portraitX    = xPositionOnScreen + 55;   // shifted right of arrow
    int portraitY    = contentTop + 10;

    // Left arrow (sits to the left of portrait)
    _leftArrowRect = new Rectangle(portraitX - 36, portraitY + portraitSize / 2 - 12, 28, 28);
    b.Draw(Game1.mouseCursors,
        new Vector2(_leftArrowRect.X, _leftArrowRect.Y),
        new Rectangle(352, 495, 12, 11),
        Color.White, 0f, Vector2.Zero, 2.3f, SpriteEffects.None, 0.9f);

    // Portrait box
    IClickableMenu.drawTextureBox(b, Game1.menuTexture,
        new Rectangle(0, 256, 60, 60),
        portraitX, portraitY, portraitSize, portraitSize,
        Color.White, drawShadow: true);

    // Portrait placeholder
    if (ModEntry.FamiliarTextures.TryGetValue(instance.FamiliarId, out var tex))
{
    b.Draw(tex,
        new Rectangle(portraitX + 8, portraitY + 8, portraitSize - 16, portraitSize - 16),
        new Rectangle(0, 128, 32, 32),
        Color.White);
}
else
{
    b.Draw(Game1.fadeToBlackRect,
        new Rectangle(portraitX + 8, portraitY + 8, portraitSize - 16, portraitSize - 16),
        Color.DimGray * 0.5f);
}
    // Right arrow (sits to the right of portrait)
    _rightArrowRect = new Rectangle(portraitX + portraitSize + 8, portraitY + portraitSize / 2 - 12, 28, 28);
    b.Draw(Game1.mouseCursors,
        new Vector2(_rightArrowRect.X, _rightArrowRect.Y),
        new Rectangle(365, 495, 12, 11),
        Color.White, 0f, Vector2.Zero, 2.3f, SpriteEffects.None, 0.9f);

    // Name scroll — below portrait
    SpriteText.drawStringWithScrollCenteredAt(b, instance.GetDisplayName(data),
        portraitX + portraitSize / 2,
        portraitY + portraitSize + 16);

    // ── Ability card — between arrows and stats ───────────
    // Sits to the right of the right arrow, left of the stats panel
    int cardX = portraitX + portraitSize + 95;  // clear of right arrow
    int cardY = contentTop + 10;
    int cardW  = 175;
    int cardH  = 220;

    IClickableMenu.drawTextureBox(b, Game1.menuTexture,
    new Rectangle(0, 256, 60, 60),
    cardX, cardY, cardW, cardH, Color.White);

if (data.Assistance?.Abilities != null &&
    data.Assistance.Abilities.Count > 0 &&
    data.Assistance.Abilities[0].Count > 0)
{
    var firstAbility = data.Assistance.Abilities[0][0];

    string abilityName = firstAbility.AbilityClass;
    string abilityDesc = "???";

    var pack = FamiliarCache.GetPackForFamiliar(instance.FamiliarId);

    if (pack != null && !string.IsNullOrEmpty(firstAbility.Description))
    {
        var translation = pack.Translation.Get(firstAbility.Description);
        if (translation.HasValue())
            abilityDesc = translation.ToString();
    }

    Utility.drawTextWithShadow(b,
        abilityName,
        Game1.smallFont,
        new Vector2(cardX + 20, cardY + 15),
        Game1.textColor);

    string wrappedDesc = Game1.parseText(
        abilityDesc,
        Game1.smallFont,
        cardW - 28);

    Utility.drawTextWithShadow(b,
        wrappedDesc,
        Game1.smallFont,
        new Vector2(cardX + 20, cardY + 48),
        Game1.textColor);
}
else
{
    Utility.drawTextWithShadow(b, "???",
        Game1.smallFont,
        new Vector2(cardX + cardW / 2 - 16, cardY + cardH / 2 - 8),
        Color.Gray);
}

    // ── Right panel: stats ───────────────────────────────
    int statsX = xPositionOnScreen + 460;
    int statsY = contentTop + 10;
    int barSpacing = 60;  // enough room for label + bar + gap

    DrawNeedBar(b, ModEntry.Translation.Get("UI.Log.Food"),      instance.Food,      statsX, statsY);
    DrawNeedBar(b, ModEntry.Translation.Get("UI.Log.Attention"), instance.Attention, statsX, statsY + barSpacing);
    DrawNeedBar(b, ModEntry.Translation.Get("UI.Log.Happiness"), instance.Happiness, statsX, statsY + barSpacing * 2,
        happinessColor: true);

    // Trust hearts — below bars with clear gap
    int trustY = statsY + barSpacing * 3 + 4;
    Utility.drawTextWithShadow(b, ModEntry.Translation.Get("UI.Log.Trust"),
        Game1.smallFont,
        new Vector2(statsX, trustY),
        Game1.textColor);
    DrawTrustHearts(b, instance.TrustHearts, statsX, trustY + 28);

    // ── Loved Gifts — pinned to bottom of menu ────────────
    int giftsY     = yPositionOnScreen + height - 90;
    int giftsSlotY = yPositionOnScreen + height - 55;

    Utility.drawTextWithShadow(b, ModEntry.Translation.Get("UI.Log.LovedGifts"),
        Game1.smallFont,
        new Vector2(xPositionOnScreen + 32, giftsY),
        Game1.textColor);

    // Get the full loved items list and revealed set
    var lovedItems = data.LovedItems ?? new List<string>();
    var revealed   = instance.RevealedLovedGifts;

    for (int i = 0; i < Math.Min(lovedItems.Count, 10); i++)
    {
        int slotX = xPositionOnScreen + 32 + i * 44;

        // Draw slot background
        b.Draw(Game1.menuTexture,
            new Rectangle(slotX, giftsSlotY, 40, 40),
            new Rectangle(64, 320, 60, 60),
            Color.White);

        string itemId = lovedItems[i];

        if (revealed.Contains(itemId))
        {
            // Draw the actual item sprite
            var item = ItemRegistry.Create(itemId, allowNull: true);
            if (item != null)
            {
                item.drawInMenu(b,
                    new Vector2(slotX - 12, giftsSlotY - 15),
                    40f / 64f, // scale to fit slot
                    1f,
                    0.9f,
                    StackDrawType.Hide,
                    Color.White,
                    false);
            }
        }
        else
        {
            // Draw a question mark for unrevealed gifts
            Utility.drawTextWithShadow(b, "?",
                Game1.smallFont,
                new Vector2(
                    slotX + 20 - Game1.smallFont.MeasureString("?").X / 2,
                    giftsSlotY + 20 - Game1.smallFont.MeasureString("?").Y / 2),
                Color.Gray);
        }
    }
}

// ─────────────────────────────────────────────────────────
// Updated DrawNeedBar — label above, bar below
// ─────────────────────────────────────────────────────────
private void DrawNeedBar(SpriteBatch b, string label, float value,
    int x, int y, bool happinessColor = false)
{
    int barWidth  = 260;
    int barHeight = 16;

    // Label on its own line
    Utility.drawTextWithShadow(b, label,
        Game1.smallFont,
        new Vector2(x, y),
        Game1.textColor);

    // Bar on the line below the label
    int barY = y + 28;

    // Background
    b.Draw(Game1.fadeToBlackRect,
        new Rectangle(x, barY, barWidth, barHeight),
        Color.DarkGray * 0.6f);

    // Fill
    Color fill = happinessColor ? GetHappinessColor(value) : GetNeedColor(value);
    int fillW = (int)(barWidth * Math.Clamp(value, 0f, 1f));
    if (fillW > 0)
        b.Draw(Game1.fadeToBlackRect,
            new Rectangle(x, barY, fillW, barHeight),
            fill * 0.9f);
}

        // ─────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────



        private void DrawCheckIcon(SpriteBatch b, bool completed, int x, int y)
{
    Rectangle checkSrc = completed
        ? new Rectangle(236, 425, 8, 8)  // green checkmark
        : new Rectangle(227, 425, 8, 8); // empty checkmark

    b.Draw(Game1.mouseCursors,
        new Vector2(x, y + 20),  // below the icon
        checkSrc,
        Color.White, 0f, Vector2.Zero, 2.5f,
        SpriteEffects.None, 0.9f);
}
        private void DrawTrustHearts(SpriteBatch b, int trustHearts, int x, int y)
        {
            for (int i = 0; i < 10; i++)
            {
                bool filled = i < trustHearts;
                Rectangle src = filled
                    ? new Rectangle(211, 428, 7, 6)
                    : new Rectangle(218, 428, 7, 6);

                b.Draw(Game1.mouseCursors,
                    new Vector2(x + i * 18, y),
                    src, Color.White, 0f, Vector2.Zero, 2.5f,
                    SpriteEffects.None, 0.9f);
            }
        }

        private Color GetNeedColor(float v) =>
            v > 0.6f ? Color.SeaGreen : v > 0.3f ? Color.Goldenrod : Color.Firebrick;

        private Color GetHappinessColor(float v) =>
            v > 0.6f ? Color.MediumPurple : v > 0.3f ? Color.Goldenrod : Color.Firebrick;
    }
}