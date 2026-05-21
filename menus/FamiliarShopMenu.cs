using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using StardewValley.BellsAndWhistles;
using StardewModdingAPI;
using System.Collections.Generic;
using System.Linq;

namespace FAUNA
{
    public class FamiliarShopMenu : IClickableMenu, IKeyboardSubscriber
    {
        // ─────────────────────────────────────────────────────────
        // Layout
        // ─────────────────────────────────────────────────────────
        private const int MenuWidth  = 800;
        private const int MenuHeight = 520;
        private const int RowHeight  = 96;
        private const int VisibleRows = 4;


        public bool Selected { get; set; }

        public void RecieveTextInput(char inputChar)
        {
            if (!_namingMode) return;
            if (_pendingName.Length >= 20) return;
            if (char.IsLetterOrDigit(inputChar) || inputChar == ' ' || 
                inputChar == '-' || inputChar == '\'')
                _pendingName += inputChar;
        }

        public void RecieveTextInput(string text)
        {
            foreach (char c in text)
                RecieveTextInput(c);
        }

        public void RecieveCommandInput(char command)
        {
            if (!_namingMode) return;
            if (command == '\b' && _pendingName.Length > 0)
                _pendingName = _pendingName[..^1];
        }

        public void RecieveSpecialInput(Keys key) { }


        // ─────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────
        private readonly FamiliarShopData _shop;
        private readonly List<FamiliarShopEntry> _stock = new();
        private int _selectedIndex = 0;
        private int _scrollOffset  = 0;

        // Shake effect on insufficient funds
        private int _shakeTimer = 0;
        private const int ShakeDuration = 30;

        // Naming popup state
        private bool _namingMode = false;
        private string _pendingName = "";
        private FamiliarShopEntry? _pendingEntry = null;
        private int _cursorBlinkTimer = 0;

        // Clickable areas
        private List<Rectangle> _rowRects = new();
        private Rectangle _buyButtonRect;
        private Rectangle _confirmButtonRect;
        private Rectangle _cancelButtonRect;

        // ─────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────
       public FamiliarShopMenu(FamiliarShopData shop) : base(
            x:      Game1.uiViewport.Width  / 2 - MenuWidth  / 2,
            y:      Game1.uiViewport.Height / 2 - MenuHeight / 2,
            width:  MenuWidth,
            height: MenuHeight,
            showUpperRightCloseButton: true)
        {
            _shop = shop;
            BuildStock(shop);
        }
        private void BuildStock(FamiliarShopData shop)
        {
            string currentSeason = Game1.currentSeason;
            foreach (var entry in shop.Stock)
            {
                if (!string.IsNullOrEmpty(entry.Season) &&
                    !entry.Season.Equals(currentSeason, 
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(entry.Condition))
                {
                    bool passes = GameStateQuery.CheckConditions(entry.Condition);
                    if (!passes) continue;
                }

                if (!string.IsNullOrEmpty(entry.RequiredMailFlag) &&
                    !Game1.player.mailReceived.Contains(entry.RequiredMailFlag))
                {
                    if (entry.HiddenUntilUnlocked)
                        continue;
                }

                _stock.Add(entry);
            }
            
        }

        // ─────────────────────────────────────────────────────────
        // Input
        // ─────────────────────────────────────────────────────────
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_namingMode)
            {
                if (_confirmButtonRect.Contains(x, y))
                {
                    ConfirmPurchase();
                    Game1.playSound("smallSelect");
                }
                else if (_cancelButtonRect.Contains(x, y))
                {
                    CancelNaming();
                    Game1.playSound("smallSelect");
                }
                return;
            }

            base.receiveLeftClick(x, y, playSound);

            // Row selection
            for (int i = 0; i < _rowRects.Count; i++)
            {
                if (_rowRects[i].Contains(x, y))
                {
                    _selectedIndex = _scrollOffset + i;
                    Game1.playSound("smallSelect");
                    return;
                }
            }

            // Buy button
            if (_buyButtonRect.Contains(x, y) && _stock.Count > 0)
            {
                TryBuy();
                return;
            }
        }

        public override void receiveScrollWheelAction(int direction)
        {
            if (_namingMode) return;
            int maxScroll = System.Math.Max(0, _stock.Count - VisibleRows);
            _scrollOffset = System.Math.Clamp(_scrollOffset - System.Math.Sign(direction), 0, maxScroll);
        }

        public override void receiveKeyPress(Keys key)
        {
            if (!_namingMode)
            {
                base.receiveKeyPress(key);
                return;
            }

            if (key == Keys.Enter || key == Keys.Escape)
            {
                if (key == Keys.Enter)
                    ConfirmPurchase();
                else
                    CancelNaming();
                return;
            }

            if (key == Keys.Back && _pendingName.Length > 0)
            {
                _pendingName = _pendingName[..^1];
                return;
            }
        }

        public override void receiveGamePadButton(Buttons b)
        {
            if (_namingMode)
            {
                if (b == Buttons.A) ConfirmPurchase();
                if (b == Buttons.B) CancelNaming();
                return;
            }
            base.receiveGamePadButton(b);
        }


        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        // ─────────────────────────────────────────────────────────
        // Update
        // ─────────────────────────────────────────────────────────
        public override void update(GameTime time)
        {
            base.update(time);

            if (_shakeTimer > 0)
                _shakeTimer--;

            _cursorBlinkTimer = (_cursorBlinkTimer + 1) % 60;

            // Hook keyboard input for naming
            if (_namingMode)
            {
                var state = Keyboard.GetState();
                // Handled via receiveKeyPress
            }
        }

        // ─────────────────────────────────────────────────────────
        // Draw
        // ─────────────────────────────────────────────────────────
        public override void draw(SpriteBatch b)
        {
            // Shake offset
            int shakeX = _shakeTimer > 0 ? Game1.random.Next(-3, 4) : 0;

            // Background dim
            b.Draw(Game1.fadeToBlackRect,
                Game1.graphics.GraphicsDevice.Viewport.Bounds,
                Color.Black * 0.5f);

            // Menu box
            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                xPositionOnScreen + shakeX, yPositionOnScreen,
                width, height, Color.White);

            // Title
            SpriteText.drawStringWithScrollCenteredAt(b, _shop.DisplayName,
                xPositionOnScreen + width / 2 + shakeX,
                yPositionOnScreen + 32);

            if (_namingMode)
            {
                DrawListPanel(b, shakeX);
                DrawDetailPanel(b, shakeX);
                base.draw(b); 
                DrawNamingOverlay(b, shakeX);
            }
            else
            {
                DrawListPanel(b, shakeX);
                DrawDetailPanel(b, shakeX);
                base.draw(b); 
            }

            drawMouse(b);
        }

        // ─────────────────────────────────────────────────────────
        // Left panel — familiar list
        // ─────────────────────────────────────────────────────────
        private void DrawListPanel(SpriteBatch b, int shakeX)
        {
            _rowRects.Clear();

            int listTop  = yPositionOnScreen + 100;
            int panelW   = width / 2 - 20;

            if (_stock.Count == 0)
            {
                Utility.drawTextWithShadow(b, ModEntry.Translation.Get("UI.Shop.NoFamiliars"),
                    Game1.smallFont,
                    new Vector2(xPositionOnScreen + 20 + shakeX, listTop + 20),
                    Game1.textColor);
                return;
            }

            for (int i = 0; i < VisibleRows; i++)
            {
                int dataIndex = _scrollOffset + i;
                if (dataIndex >= _stock.Count) break;

                var entry = _stock[dataIndex];

                // Temporary case-insensitive fallback to test if casing is the issue
                ModEntry.RegisteredFamiliars.TryGetValue(entry.FamiliarId, out var data);

                if (data == null)
                    continue;

                bool locked = !string.IsNullOrEmpty(entry.RequiredMailFlag) &&
                              !Game1.player.mailReceived.Contains(entry.RequiredMailFlag);

                int rowY = listTop + i * RowHeight;
                var rowRect = new Rectangle(
                    xPositionOnScreen + 20 + shakeX, rowY,
                    panelW - 20, RowHeight - 4);
                _rowRects.Add(rowRect);

                bool selected = dataIndex == _selectedIndex;
                bool hovered  = rowRect.Contains(Game1.getMouseX(), Game1.getMouseY());

                Color rowTint = locked ? Color.Gray * 0.6f :
                                selected ? Color.Wheat : Color.White;

                IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height,
                    rowTint, drawShadow: false);

                // Sprite
                if (ModEntry.FamiliarTextures.TryGetValue(entry.FamiliarId, out var tex))
                {
                    b.Draw(tex,
                        new Rectangle(rowRect.X + 8, rowRect.Y + 8, 72, 72),
                        new Rectangle(0, 0, 32, 32), // always use first sprite
                        locked ? Color.Gray * 0.5f : Color.White);
                }

                // Name
                Utility.drawTextWithShadow(b, data.DisplayName,
                    Game1.dialogueFont,
                    new Vector2(rowRect.X + 88, rowRect.Y + 15),
                    locked ? Color.Gray : Game1.textColor);

                // Price
                Utility.drawTextWithShadow(b, $"{entry.Price}g",
                    Game1.smallFont,
                    new Vector2(rowRect.X + 88, rowRect.Y + 47),
                    locked ? Color.Gray : Color.SaddleBrown);

                // Locked label
                if (locked)
                {
                    Utility.drawTextWithShadow(b, ModEntry.Translation.Get("UI.Shop.Locked"),
                        Game1.smallFont,
                        new Vector2(rowRect.Right - 80, rowRect.Y + 36),
                        Color.Gray);
                }
            }
        }

        // ─────────────────────────────────────────────────────────
        // Right panel — detail + buy
        // ─────────────────────────────────────────────────────────
        private void DrawDetailPanel(SpriteBatch b, int shakeX)
        {
            if (_stock.Count == 0) return;
            _selectedIndex = System.Math.Clamp(_selectedIndex, 0, _stock.Count - 1);

            var entry = _stock[_selectedIndex];
            if (!ModEntry.RegisteredFamiliars.TryGetValue(entry.FamiliarId, out var data))
                return;

            bool locked = !string.IsNullOrEmpty(entry.RequiredMailFlag) &&
                          !Game1.player.mailReceived.Contains(entry.RequiredMailFlag);

            int panelX   = xPositionOnScreen + width / 2 + 10 + shakeX;
            int panelTop = yPositionOnScreen + 100;

            // Portrait box
            int portraitSize = 128;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                panelX, panelTop, portraitSize, portraitSize,
                Color.White);

            if (ModEntry.FamiliarTextures.TryGetValue(entry.FamiliarId, out var tex))
            {
                b.Draw(tex,
                    new Rectangle(panelX + 8, panelTop + 8, portraitSize - 16, portraitSize - 16),
                    new Rectangle(0, 0, 32, 32), // always use first sprite
                    Color.White);
            }

            // Ability card
            int cardX = panelX + portraitSize + 16;
            int cardY = panelTop;
            int cardW = width / 2 - portraitSize - 36;
            int cardH = 250;

            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                cardX, cardY, cardW, cardH, Color.White);

            string abilityName = "";
            string abilityDesc = "";

            // Try to get ability name + description from first ability
            if (data.Assistance?.Abilities != null &&
                data.Assistance.Abilities.Count > 0 &&
                data.Assistance.Abilities[0].Count > 0)
            {
                var firstAbility = data.Assistance.Abilities[0][0];
                abilityName = firstAbility.AbilityClass;

                if (!string.IsNullOrEmpty(firstAbility.Description))
                    abilityDesc = firstAbility.Description;
            }

            // Fallback 1: familiar's own description
            if (string.IsNullOrEmpty(abilityDesc) && !string.IsNullOrEmpty(data.Description))
                abilityDesc = data.Description;

            // Fallback 2: ??? only if everything failed
            if (string.IsNullOrEmpty(abilityDesc))
                abilityDesc = "???";

            if (!string.IsNullOrEmpty(abilityName))
                Utility.drawTextWithShadow(b, abilityName, Game1.smallFont,
                    new Vector2(cardX + 16, cardY + 16), Game1.textColor);

            string wrappedDesc = Game1.parseText(abilityDesc, Game1.smallFont, cardW - 24);
            Utility.drawTextWithShadow(b, wrappedDesc, Game1.smallFont,
                new Vector2(cardX + 16, cardY + (string.IsNullOrEmpty(abilityName) ? 16 : 48)),
                Game1.textColor);

            // Buy button
            int buyY = yPositionOnScreen + height - 80;
            _buyButtonRect = new Rectangle(
            xPositionOnScreen + width - 180 + shakeX, 
            yPositionOnScreen + height - 80,
            160, 48);

            bool canAfford = Game1.player.Money >= entry.Price;
            Color buyColor = (locked || !canAfford) ? Color.Gray * 0.7f : Color.White;

            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                _buyButtonRect.X, _buyButtonRect.Y,
                _buyButtonRect.Width, _buyButtonRect.Height,
                buyColor);

            Utility.drawTextWithShadow(b,
                locked 
                    ? ModEntry.Translation.Get("UI.Shop.LockedButton").ToString()
                    : ModEntry.Translation.Get("UI.Shop.Buy", new { price = entry.Price }).ToString(),
                Game1.smallFont,
                new Vector2(_buyButtonRect.X + 16, _buyButtonRect.Y + 10),
                locked || !canAfford ? Color.Gray : Game1.textColor);
        }

        // ─────────────────────────────────────────────────────────
        // Naming overlay
        // ─────────────────────────────────────────────────────────
        private void DrawNamingOverlay(SpriteBatch b, int shakeX)
        {
            if (_pendingEntry == null) return;
            if (!ModEntry.RegisteredFamiliars.TryGetValue(
                _pendingEntry.FamiliarId, out var data)) return;

        // Draw full screen dim 
        b.Draw(Game1.fadeToBlackRect,
            Game1.graphics.GraphicsDevice.Viewport.Bounds,
            Color.Black);

            int boxW = 400;
            int boxH = 220;
            int boxX = xPositionOnScreen + width / 2 - boxW / 2 + shakeX;
            int boxY = yPositionOnScreen + height / 2 - boxH / 2;

            // Overlay box
            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                boxX, boxY, boxW, boxH, Color.White);

            // prompt scroll 
            SpriteText.drawStringWithScrollCenteredAt(b,
                $"Name your {data.DisplayName}?",
                boxX + boxW / 2, boxY + 16);

            // input field 
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle(boxX + 20, boxY + 96, boxW - 40, 40),
                Color.Black * 0.3f);

            string display = _pendingName + (_cursorBlinkTimer < 30 ? "|" : " ");
            Utility.drawTextWithShadow(b, display,
                Game1.dialogueFont,
                new Vector2(boxX + 28, boxY + 94),
                Game1.textColor);

            // Confirm and Cancel buttons
            int btnY = boxY + boxH - 60;
            int btnW = 120;
            int btnH = 44;

            // Confirm button
            Rectangle confirmRect = new Rectangle(boxX + 20, btnY, btnW, btnH);
            bool confirmHovered = confirmRect.Contains(Game1.getMouseX(), Game1.getMouseY());
            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                confirmRect.X, confirmRect.Y, confirmRect.Width, confirmRect.Height,
                confirmHovered ? Color.Wheat : Color.White);
            Utility.drawTextWithShadow(b, ModEntry.Translation.Get("UI.Shop.Confirm"),
                Game1.smallFont,
                new Vector2(confirmRect.X + 14, confirmRect.Y + 12),
                Game1.textColor);

            // Cancel button
            Rectangle cancelRect = new Rectangle(boxX + boxW - btnW - 20, btnY, btnW, btnH);
            bool cancelHovered = cancelRect.Contains(Game1.getMouseX(), Game1.getMouseY());
            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                cancelRect.X, cancelRect.Y, cancelRect.Width, cancelRect.Height,
                cancelHovered ? Color.Wheat : Color.White);
            Utility.drawTextWithShadow(b, ModEntry.Translation.Get("UI.Shop.Cancel"),
                Game1.smallFont,
                new Vector2(cancelRect.X + 18, cancelRect.Y + 12),
                Game1.textColor);

            // Store rects for click handling
            _confirmButtonRect = confirmRect;
            _cancelButtonRect  = cancelRect;
        }

        // ─────────────────────────────────────────────────────────
        // Purchase logic
        // ─────────────────────────────────────────────────────────
        private void TryBuy()
        {
            if (_stock.Count == 0) return;
            var entry = _stock[_selectedIndex];

            // Locked check
            if (!string.IsNullOrEmpty(entry.RequiredMailFlag) &&
                !Game1.player.mailReceived.Contains(entry.RequiredMailFlag))
            {
                Shake();
                Game1.playSound("cancel");
                return;
            }

            // Funds check
            if (Game1.player.Money < entry.Price)
            {
                Shake();
                Game1.playSound("cancel");
                return;
            }

            // Open naming
            _pendingEntry = entry;
            _pendingName  = "";
            _namingMode   = true;
            Game1.playSound("smallSelect");

            // Hook text input
            Game1.keyboardDispatcher.Subscriber = this as IKeyboardSubscriber;
        }

        private void ConfirmPurchase()
        {
            if (_pendingEntry == null) return;

            string finalName = string.IsNullOrWhiteSpace(_pendingName)
                ? "" // empty = use species name
                : _pendingName.Trim();

            // Deduct money
            Game1.player.Money -= _pendingEntry.Price;

            // Add familiar
            var instance = ModEntry.FamiliarManager!.AddFamiliar(
                _pendingEntry.FamiliarId, finalName);

            if (instance != null)
            {
                // Invalidate den map so new room stitches in
                ModEntry.ModHelper.GameContent.InvalidateCache(
                    "Maps/FAUNA.FamiliarDen");

                // Respawn so the new familiar appears immediately
                ModEntry.FamiliarManager.SpawnFamiliars();

                Game1.playSound("purchase");
                Game1.drawObjectDialogue(
                    $"Welcome, {instance.GetDisplayName(ModEntry.RegisteredFamiliars[_pendingEntry.FamiliarId])}!");
            }

            CancelNaming();
        }

        private void CancelNaming()
        {
            _namingMode   = false;
            _pendingEntry = null;
            _pendingName  = "";
        }

        private void Shake()
        {
            _shakeTimer = ShakeDuration;
        }
    }
}