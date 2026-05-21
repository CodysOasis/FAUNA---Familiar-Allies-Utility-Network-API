using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FAUNA
{
    public class FamiliarInventoryMenu : IClickableMenu
    {
        // ─────────────────────────────────────────────────────────
        // Layout
        // ─────────────────────────────────────────────────────────
        private const int SlotSize    = 64;
        private const int SlotPadding = 8;
        private const int SlotsPerRow = 10;
        private const int MenuPadding = 32;

        // ─────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────
        private readonly FamiliarInstance _instance;
        private readonly FamiliarData     _data;
        private readonly bool             _allowDeposit;
        private readonly int              _inventorySize;

        private List<Rectangle> _slotRects = new();
        private int _hoveredSlot = -1;

        // ─────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────
        public FamiliarInventoryMenu(
            FamiliarInstance instance,
            FamiliarData data)
        {
            _instance      = instance;
            _data          = data;
            _allowDeposit  = data.Assistance?.AllowDeposit ?? false;
            _inventorySize = data.Assistance?.InventorySize ?? 0;

            // Calculate menu size based on inventory slots
            int rows   = (int)Math.Ceiling(_inventorySize / (float)SlotsPerRow);
            int menuW  = SlotsPerRow * (SlotSize + SlotPadding) + MenuPadding * 2;
            int menuH  = rows * (SlotSize + SlotPadding) + MenuPadding * 2 + 85; // +85 for title

            initialize(
                Game1.uiViewport.Width  / 2 - menuW / 2,
                Game1.uiViewport.Height / 2 - menuH / 2,
                menuW,
                menuH,
                showUpperRightCloseButton: true);

            LayoutSlots();
        }

        private void LayoutSlots()
        {
            _slotRects.Clear();
            int startX = xPositionOnScreen + MenuPadding;
            int startY = yPositionOnScreen + 80;

            for (int i = 0; i < _inventorySize; i++)
            {
                int col = i % SlotsPerRow;
                int row = i / SlotsPerRow;
                _slotRects.Add(new Rectangle(
                    startX + col * (SlotSize + SlotPadding),
                    startY + row * (SlotSize + SlotPadding),
                    SlotSize,
                    SlotSize));
            }
        }

        // ─────────────────────────────────────────────────────────
        // Input
        // ─────────────────────────────────────────────────────────
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            for (int i = 0; i < _slotRects.Count; i++)
            {
                if (!_slotRects[i].Contains(x, y)) continue;

                // Clicking an occupied slot — take the item
                if (i < _instance.Inventory.Count)
                {
                    string itemId = _instance.Inventory[i];
                    var item = ItemRegistry.Create(itemId, allowNull: true);

                    if (item != null)
                    {
                        // Try to add to player inventory
                        var leftover = Game1.player.addItemToInventory(item);
                        if (leftover == null)
                        {
                            // Successfully taken
                            _instance.Inventory.RemoveAt(i);
                            Game1.playSound("coin");
                        }
                        else
                        {
                            // Player inventory full
                            Game1.showRedMessage(
                                ModEntry.Translation.Get(
                                    "UI.Inventory.Full").ToString());
                        }
                    }
                }
                else if (_allowDeposit && Game1.player.CurrentItem != null)
                {
                    // Empty slot + deposit allowed — add held item
                    if (_instance.Inventory.Count < _inventorySize)
                    {
                        _instance.Inventory.Add(
                            Game1.player.CurrentItem.QualifiedItemId);
                        Game1.player.CurrentItem.Stack--;
                        if (Game1.player.CurrentItem.Stack <= 0)
                            Game1.player.removeItemFromInventory(
                                Game1.player.CurrentItem);
                        Game1.playSound("coin");
                    }
                }

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

            // Title
            string name = _instance.GetDisplayName(_data);
            StardewValley.BellsAndWhistles.SpriteText
                .drawStringWithScrollCenteredAt(b, name,
                    xPositionOnScreen + width / 2,
                    yPositionOnScreen + 20);

            // Slots
            _hoveredSlot = -1;
            for (int i = 0; i < _slotRects.Count; i++)
            {
                var rect = _slotRects[i];
                bool hovered = rect.Contains(Game1.getMouseX(), Game1.getMouseY());
                if (hovered) _hoveredSlot = i;

                // Slot background
                b.Draw(Game1.menuTexture,
                    new Rectangle(rect.X, rect.Y, rect.Width, rect.Height),
                    new Rectangle(64, 325, 60, 60),
                    hovered ? Color.Wheat : Color.White);

                // Item in slot
                if (i < _instance.Inventory.Count)
                {
                    var item = ItemRegistry.Create(
                        _instance.Inventory[i], allowNull: true);

                    item?.drawInMenu(b,
                        new Vector2(rect.X - 8, rect.Y - 8),
                        (float)SlotSize / 64f,
                        1f,
                        0.9f,
                        StackDrawType.Draw,
                        Color.White,
                        false);
                }
                else
                {
                    // Empty slot indicator
                    if (_allowDeposit)
                    {
                        // Show a faint + if deposit is allowed
                        Utility.drawTextWithShadow(b, "+",
                            Game1.smallFont,
                            new Vector2(
                                rect.X + rect.Width  / 2 - 
                                    Game1.smallFont.MeasureString("+").X / 2,
                                rect.Y + rect.Height / 2 - 
                                    Game1.smallFont.MeasureString("+").Y / 2),
                            Color.Gray * 0.5f);
                    }
                }
            }

            // Tooltip for hovered item
            if (_hoveredSlot >= 0 && _hoveredSlot < _instance.Inventory.Count)
            {
                var item = ItemRegistry.Create(
                    _instance.Inventory[_hoveredSlot], allowNull: true);
                if (item != null)
                    drawToolTip(b, item.getDescription(), item.DisplayName, item);
            }

            // Deposit hint
            if (_allowDeposit)
            {
                Utility.drawTextWithShadow(b,
                    ModEntry.Translation.Get("UI.Inventory.DepositHint").ToString(),
                    Game1.smallFont,
                    new Vector2(
                        xPositionOnScreen + MenuPadding,
                        yPositionOnScreen + height - 40),
                    Color.Gray);
            }

            base.draw(b);
            drawMouse(b);
        }
    }
}