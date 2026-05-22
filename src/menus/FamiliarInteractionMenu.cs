using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using System;

namespace FAUNA
{
    public class FamiliarInteractionMenu : IClickableMenu
    {
        // ─────────────────────────────────────────────────────────
        // Layout
        // ─────────────────────────────────────────────────────────
        private const int MenuWidth  = 400;
        private const int MenuHeight = 380;
        private int GetMenuHeight() =>
            (_data.Assistance?.InventorySize ?? 0) > 0 ? 380 : 320;
        private const int ButtonHeight = 64;
        private const int ButtonPadding = 8;

        // ─────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────
        private readonly FamiliarInstance _instance;
        private readonly FamiliarData _data;
        private readonly FamiliarEntity _entity;
        private readonly Farmer _who;

        private Rectangle _chatButton;
        private Rectangle _followButton;
        private Rectangle _cancelButton;
        private Rectangle _bagButton;

        // ─────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────
        public FamiliarInteractionMenu(
            Farmer who,
            FamiliarInstance instance,
            FamiliarData data,
            FamiliarEntity entity)
            : base(
                x:      Game1.uiViewport.Width  / 2 - MenuWidth  / 2,
                y:      Game1.uiViewport.Height / 2 - MenuHeight / 2,
                width:  MenuWidth,
                height: MenuHeight,
                showUpperRightCloseButton: true)
        {
            _who      = who;
            _instance = instance;
            _data     = data;
            _entity   = entity;

            LayoutButtons();
        }

        private void LayoutButtons()
        {
            int btnX  = xPositionOnScreen + 20;
            int btnW  = width - 40;
            int startY = yPositionOnScreen + 80;
            bool hasBag = (_data.Assistance?.InventorySize ?? 0) > 0;

            _chatButton   = new Rectangle(btnX, startY, btnW, ButtonHeight);
            _followButton = new Rectangle(btnX, startY + ButtonHeight + ButtonPadding, btnW, ButtonHeight);

            if (hasBag)
            {
                _bagButton    = new Rectangle(btnX, startY + (ButtonHeight + ButtonPadding) * 2, btnW, ButtonHeight);
                _cancelButton = new Rectangle(btnX, startY + (ButtonHeight + ButtonPadding) * 3, btnW, ButtonHeight);
            }
            else
            {
                _cancelButton = new Rectangle(btnX, startY + (ButtonHeight + ButtonPadding) * 2, btnW, ButtonHeight);
            }
        }

        // ─────────────────────────────────────────────────────────
        // Input
        // ─────────────────────────────────────────────────────────
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_chatButton.Contains(x, y))
            {
                Game1.playSound("smallSelect");
                Game1.activeClickableMenu = null;
                _entity.HandleChat(_who, _instance, _data);
                return;
            }

            if (_followButton.Contains(x, y))
            {
                Game1.playSound("smallSelect");
                Game1.activeClickableMenu = null;
                _entity.SetState(
                    _entity.CurrentState == FamiliarState.Following
                        ? FamiliarState.Passive
                        : FamiliarState.Following);
                return;
            }

            if (_cancelButton.Contains(x, y))
            {
                Game1.playSound("smallSelect");
                Game1.activeClickableMenu = null;
                return;
            }

            if ((_data.Assistance?.InventorySize ?? 0) > 0 && 
                _bagButton.Contains(x, y))
            {
                Game1.playSound("smallSelect");
                Game1.activeClickableMenu = new FamiliarInventoryMenu(_instance, _data);
                return;
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
                Game1.activeClickableMenu = null;
        }

        // ─────────────────────────────────────────────────────────
        // Draw
        // ─────────────────────────────────────────────────────────
        public override void draw(SpriteBatch b)
        {
            // Dim background
            b.Draw(Game1.fadeToBlackRect,
                Game1.graphics.GraphicsDevice.Viewport.Bounds,
                Color.Black * 0.4f);

            // Menu box
            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                xPositionOnScreen, yPositionOnScreen,
                width, height, Color.White);

            // Title — familiar name
            string name = _instance.GetDisplayName(_data);
            StardewValley.BellsAndWhistles.SpriteText.drawStringWithScrollCenteredAt(
                b, name,
                xPositionOnScreen + width / 2,
                yPositionOnScreen + 20);

            // Buttons
            DrawButton(b, _chatButton, 
                ModEntry.Translation.Get("UI.Interaction.Chat"));
            DrawButton(b, _followButton,
                _entity.CurrentState == FamiliarState.Following
                    ? ModEntry.Translation.Get("UI.Interaction.StopFollowing")
                    : ModEntry.Translation.Get("UI.Interaction.FollowMe"));
            DrawButton(b, _cancelButton, 
                ModEntry.Translation.Get("UI.Interaction.NeverMind"));
            if ((_data.Assistance?.InventorySize ?? 0) > 0)
            {
                DrawButton(b, _bagButton,
                    ModEntry.Translation.Get("UI.Interaction.CheckBag").ToString());
            }

            base.draw(b);
            drawMouse(b);
        }

        private void DrawButton(SpriteBatch b, Rectangle rect, string label)
        {
            bool hovered = rect.Contains(Game1.getMouseX(), Game1.getMouseY());

            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                rect.X, rect.Y, rect.Width, rect.Height,
                hovered ? Color.Wheat : Color.White,
                drawShadow: false);

            Utility.drawTextWithShadow(b, label,
                Game1.dialogueFont,
                new Vector2(
                    rect.X + rect.Width  / 2 - Game1.dialogueFont.MeasureString(label).X / 2,
                    rect.Y + rect.Height / 2 - Game1.dialogueFont.MeasureString(label).Y / 2 +8),
                Game1.textColor);
        }
    }
}