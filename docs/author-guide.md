# FAUNA — Author Guide

FAUNA content packs are standard **Content Patcher** packs. No C# required — just JSON, sprites, and the CP workflow you already know.

If you're not familiar with Content Patcher basics, read the [CP author guide](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide.md) first.

---

## Requirements

Your pack's `manifest.json` should declare CP as its framework and FAUNA as a dependency:

```jsonc
{
    "Name": "Your Pack Name",
    "Author": "YourName",
    "Version": "1.0.0",
    "Description": "A FAUNA content pack.",
    "UniqueID": "YourName.YourModName",
    "ContentPackFor": {
        "UniqueID": "Pathoschild.ContentPatcher"
    },
    "Dependencies": [
        {
            "UniqueID": "Pathoschild.ContentPatcher",
            "IsRequired": true
        },
        {
            "UniqueID": "CodysOasis.FAUNA",
            "IsRequired": true,
            "MinimumVersion": "1.1.0"
        }
    ]
}
```

---

## Pack Structure

```
[FAUNA] YourMod/
├── manifest.json
├── content.json
├── i18n/
│   └── default.json
└── assets/
    ├── data/
    │   ├── familiars.json
    │   ├── shops.json
    │   ├── textures.json
    │   └── dialogue/
    │       ├── familiar_one.json
    │       └── familiar_two.json
    ├── sprites/
    ├── portraits/
    ├── icons/
    └── dialogue/
        └── blank.json
```
---

## content.json

Your `content.json` uses `Action: Include` to keep things organized:

```jsonc
{
    "Format": "2.9.1",
    "Changes": [
        { "Action": "Include", "FromFile": "assets/data/familiars.json" },
        { "Action": "Include", "FromFile": "assets/data/shops.json" },
        { "Action": "Include", "FromFile": "assets/data/textures.json" },
        { "Action": "Include", "FromFile": "assets/data/dialogue/familiar_one.json" },
        { "Action": "Include", "FromFile": "assets/data/dialogue/familiar_two.json" }
    ]
}
```

---

## Registering Familiars

Familiars are registered by editing the `Mods/CodysOasis.FAUNA/Familiars` game asset.  
The dictionary key is the FamiliarId — use `{{ModId}}` so it's automatically namespaced.

**`assets/data/familiars.json`:**
```jsonc
{
    "Format": "2.9.1",
    "Changes": [
        {
            "Action": "EditData",
            "Target": "Mods/CodysOasis.FAUNA/Familiars",
            "Entries": {
                "{{ModId}}_YourFamiliar": {
                    "DisplayName": "{{i18n:YourFamiliar.name}}",
                    "Description": "{{i18n:YourFamiliar.desc}}",
                    "FamiliarType": "Foraging",
                    "Diet": "Omnivore",

                    "SpriteAsset": "Mods/{{ModId}}/Sprites/YourFamiliar",
                    "PortraitAsset": "Mods/{{ModId}}/Portraits/YourFamiliar",
                    "DialogueAsset": "Mods/{{ModId}}/Dialogue/YourFamiliar",
                    "Speaks": true,
                    "AlwaysAnimate": false,
                    "AnimateInterval": 80,

                    "ExcludeFromDefaultShop": false,
                    "DefaultShopPrice": 2000,

                    "Needs": {
                        "FoodDecayPerDay": 0.2,
                        "AttentionDecayPerDay": 0.25
                    },

                    "Assistance": {
                        "BuffIcon": "Mods/{{ModId}}/Icons/YourFamiliar",
                        "InventorySize": 0,
                        "AllowDeposit": false,
                        "Abilities": [
                            [
                                {
                                    "Id": "{{ModId}}_YourFamiliar_Ability",
                                    "AbilityClass": "ForageHarvest",
                                    "Description": "{{i18n:YourFamiliar.ability.desc}}",
                                    "Proc": "OnNearby",
                                    "ProcTimer": 60.0,
                                    "ProcAnimation": true,
                                    "Args": { "Range": "320" }
                                }
                            ]
                        ]
                    },

                    "LovedItems": [ "(O)724" ],
                    "HatedItems": [ "(O)167" ],
                    "DropsLoot": false,
                    "LootPool": []
                }
            }
        }
    ]
}
```

---

## FamiliarData Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `DisplayName` | string | *(required)* | Name shown in-game. Supports `{{i18n:key}}`. |
| `Description` | string | `""` | Short text shown in menus. Supports `{{i18n:key}}`. |
| `FamiliarType` | string | `""` | Informal category label e.g. `"Combat"`, `"Foraging"`, `"Farming"`, `"Social"`. |
| `Diet` | DietType | `Omnivore` | One of: `Carnivore`, `Herbivore`, `Omnivore`. |
| `SpriteAsset` | string | *(required)* | Game asset path for the spritesheet. |
| `PortraitAsset` | string | `""` | Game asset path for the portrait. Optional. |
| `DialogueAsset` | string | `""` | Game asset path for the dialogue dictionary. Required if `Speaks: true`. |
| `RoomAsset` | string | `""` | Game asset path for a custom den room TMX. Optional — falls back to default room. |
| `Speaks` | bool | `true` | Whether the familiar shows dialogue when interacted with. |
| `AlwaysAnimate` | bool | `false` | If `true`, animates even when idle. Use for flying familiars. |
| `AnimateInterval` | float | `80` | Milliseconds per frame when idle-animating. Lower = faster. Only used if `AlwaysAnimate: true`. |
| `ExcludeFromDefaultShop` | bool | `false` | If `true`, won't appear in the Ouija Board shop. |
| `DefaultShopPrice` | int | `5000` | Price in the Ouija Board shop. |
| `Needs` | NeedsData | *(see below)* | Daily stat decay rates. |
| `LovedItems` | string[] | `[]` | Qualified item IDs the familiar loves as gifts. |
| `HatedItems` | string[] | `[]` | Qualified item IDs the familiar hates as gifts. |
| `Assistance` | AssistanceData | *(see below)* | Ability configuration. |

> **Not yet implemented:** `HasHumanoid`, `HumanoidSpriteAsset`, `HumanoidPortraitAsset`, `HumanoidDialogueAsset` — defined in the schema for forward compatibility but currently have no effect. `DropsLoot` and `LootPool` are also reserved for a future update.

---

## NeedsData

| Field | Default | Description |
|-------|---------|-------------|
| `FoodDecayPerDay` | `0.2` | Food need decay per day. `0.0`–`1.0` scale. |
| `AttentionDecayPerDay` | `0.25` | Attention need decay per day. |

> If `Speaks: false`, set `AttentionDecayPerDay` to `0` — the only other way to raise attention is gifting.

---

## Diet Types

| Value | Accepts |
|-------|---------|
| `Carnivore` | Meat, fish, animal products |
| `Herbivore` | Fruits, vegetables, forage |
| `Omnivore` | All food items |

---

## AssistanceData

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `BuffIcon` | string | `""` | Game asset path for buff bar icon. Should be a single image — FAUNA always uses index 0. Falls back to FAUNA default if empty. |
| `InventorySize` | int | `0` | Familiar inventory slots. `0` = no inventory. |
| `AllowDeposit` | bool | `false` | If `true`, player can put items into the familiar's inventory. |
| `Abilities` | List\<List\<AbilityData\>\> | `[]` | Abilities by trust tier. See below. |

### Ability Tiers

Each index corresponds to a trust tier. Abilities within a tier all run simultaneously.

| Index | Hearts Required |
|-------|----------------|
| `0` | 0 hearts |
| `1` | 2 hearts |
| `2` | 4 hearts |
| `3` | 6 hearts |
| `4` | 8 hearts |
| `5` | 10 hearts |

Pass `[]` for tiers with no new abilities. You don't need to define all 6 — FAUNA falls back to the highest defined tier.

---

## AbilityData

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Id` | string | *(required)* | Unique ability ID. Use `{{ModId}}_FamiliarName_AbilityName`. |
| `AbilityClass` | string | `"Nop"` | What the ability does. See below. |
| `Description` | string | `""` | UI description. Supports `{{i18n:key}}`. |
| `Proc` | string | `"OnFollow"` | `"OnFollow"` (timed, while following) or `"OnNearby"` (target in range). |
| `ProcTimer` | float | `60.0` | Cooldown in real-time seconds. `-1` = no cooldown. |
| `Condition` | string | `""` | GSQ condition that must pass before proc. |
| `ProcSound` | string | `""` | Sound cue on proc. |
| `ProcAnimation` | bool | `false` | If `true`, plays Row 10 proc animation (frames 36–39). |
| `Args` | Dictionary\<string, string\> | `{}` | Ability-specific arguments. See below. |

### Ability Classes

| AbilityClass | Description | Args |
|--------------|-------------|------|
| `Buff` | Applies a vanilla buff | `"BuffId": "luck"`, `"Magnitude": "1"` |
| `Heal` | Restores player health | `"Amount": "10"` |
| `Energy` | Restores player energy | `"Amount": "10"` |
| `AnimalFriendship` | Boosts friendship with nearby farm animals | `"Range": "256"`, `"Amount": "15"` |
| `NPCFriendship` | Boosts friendship with nearby NPCs | `"Range": "256"`, `"Amount": "15"` |
| `FamiliarFriendship` | Boosts trust with nearby familiars | `"Range": "256"`, `"Amount": "15"` |
| `Melee` | Attacks nearby monsters | `"Range": "128"`, `"Damage": "5"`, `"Knockback": "5"` |
| `Fisher` | Assists with fishing | `"Range": "384"` |
| `CropHarvest` | Harvests nearby mature crops | `"Range": "320"` |
| `AnimalHarvest` | Collects products from nearby farm animals | `"Range": "320"` |
| `ForageHarvest` | Collects nearby forage | `"Range": "320"` |
| `Nop` | Does nothing. Useful as a placeholder. | *(none)* |

---

## Registering Assets

All textures and dialogue are registered as CP game assets. Add `Load` patches in `assets/data/textures.json`:

```jsonc
{
    "Format": "2.9.1",
    "Changes": [
        { "Action": "Load", "Target": "Mods/{{ModId}}/Sprites/YourFamiliar", "FromFile": "assets/sprites/your_familiar.png" },
        { "Action": "Load", "Target": "Mods/{{ModId}}/Portraits/YourFamiliar", "FromFile": "assets/portraits/your_familiar.png" },
        { "Action": "Load", "Target": "Mods/{{ModId}}/Icons/YourFamiliar", "FromFile": "assets/icons/your_familiar_buff.png" },
        { "Action": "Load", "Target": "Mods/{{ModId}}/Rooms/YourFamiliar", "FromFile": "assets/maps/rooms/your_familiar_room.tmx" }
    ]
}
```

---

## Sprite Sheet Requirements

**Sprites** — 32×32px per frame, PNG with transparent background.

| Row | Content |
|-----|---------|
| 1 | Walk Down |
| 2 | Walk Right |
| 3 | Walk Up |
| 4 | Walk Left |
| 5–9 | Reserved |
| 10 | Proc animation (frames 36–39) |

**Portraits** — 64×64px per frame, PNG with transparent background. Only shown if `Speaks: true`.

---

## Dialogue

Dialogue files are registered as CP game assets using a `blank.json` seed + `EditData` pattern. This lets CP resolve `{{i18n:key}}` tokens in your dialogue strings.

**`assets/dialogue/blank.json`:**
```json
{}
```

**`assets/data/dialogue/your_familiar.json`:**
```jsonc
{
    "Format": "2.9.1",
    "Changes": [
        {
            "Action": "Load",
            "Target": "Mods/{{ModId}}/Dialogue/YourFamiliar",
            "FromFile": "assets/dialogue/blank.json"
        },
        {
            "Action": "EditData",
            "Target": "Mods/{{ModId}}/Dialogue/YourFamiliar",
            "Entries": {
                "Chat_Trust0_Happy_0": "{{i18n:YourFamiliar_Chat_Trust0_Happy_0}}",
                "Chat_Trust0_Happy_1": "{{i18n:YourFamiliar_Chat_Trust0_Happy_1}}",
                "Chat_Trust0_Sad_0": "{{i18n:YourFamiliar_Chat_Trust0_Sad_0}}"
            }
        }
    ]
}
```

### Dialogue Key Format
{Action}{TrustTier}{Mood}_{Index}

| Segment | Values |
|---------|--------|
| `Action` | `Chat`, `Feed`, `Gift`, `Idle`, `IdleResponse` |
| `TrustTier` | `Trust0`, `Trust2`, `Trust4`, `Trust6`, `Trust8`, `Trust10` |
| `Mood` | `Happy`, `Sad` |
| `Index` | `0`, `1`, `2`, ... (FAUNA picks randomly from available lines) |

---

## Shops (Optional)

Register a custom shop via the `Mods/CodysOasis.FAUNA/Shops` asset.

**`assets/data/shops.json`:**
```jsonc
{
    "Format": "2.9.1",
    "Changes": [
        {
            "Action": "EditData",
            "Target": "Mods/CodysOasis.FAUNA/Shops",
            "Entries": {
                "{{ModId}}_YourShop": {
                    "ShopId": "{{ModId}}_YourShop",
                    "DisplayName": "{{i18n:shop.name}}",
                    "ShopOwner": "NpcInternalName",
                    "Stock": [
                        {
                            "FamiliarId": "{{ModId}}_YourFamiliar",
                            "Price": 2000,
                            "Condition": "PLAYER_FRIENDSHIP_POINTS Current NpcInternalName 500"
                        }
                    ]
                }
            }
        }
    ]
}
```

To open your shop via a map tile, add an `Action` tile property in Tiled on the `Buildings` layer object:
FAUNA.OpenFamiliarShop {{ModId}}_YourShop

---

## i18n

All text fields that support `{{i18n:key}}` are resolved by CP before FAUNA reads them — this includes `DisplayName`, `Description`, and ability `Description` fields, as well as dialogue entries.

**`i18n/default.json`:**
```json
{
    "YourFamiliar.name": "Your Familiar",
    "YourFamiliar.desc": "A mysterious creature.",
    "YourFamiliar.ability.desc": "Collects nearby forage while following you.",
    "YourFamiliar_Chat_Trust0_Happy_0": "Hello there."
}
```

---

## Example Pack

The full source for **[TotAG] Vael's Familiars** is included in this repo under [`examplepack/`](../examplepack/%5BFAUNA%5DVaelsFamiliars) — 11 familiars across multiple species, with shops, dialogue, portraits, and i18n. Use it as a reference for any of the above.

---

## Tips

- Use `{{ModId}}` everywhere for FamiliarIds, asset paths, and shop IDs — it namespaces everything automatically and prevents conflicts with other packs.
- Familiar IDs in player saves are the fully resolved key (e.g. `CodysOasis.TotAG_FAUNA_VaelsFamiliars_BlackCat`) — changing a FamiliarId after release will break existing saves for players who own that familiar.
- `ProcTimer` is in real-time seconds, not game ticks. `AnimalFriendship` and similar social abilities are additionally capped at once per animal per day regardless of `ProcTimer`.
- If your familiar has no abilities yet, pass `[]` for the whole `Abilities` list — the shop card will fall back to the familiar's `Description` instead of showing `???`.
- Run `patch summary` in the SMAPI console to verify your patches are applying correctly
