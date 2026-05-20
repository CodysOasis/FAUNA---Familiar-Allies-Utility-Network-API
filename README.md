# FAUNA — Familiar Allies Utility Network API

> *Something stirs at the edges of Pelican Town. Not a monster. Not quite an animal. Something older — a creature that watches, learns, and chooses.*

**FAUNA** is a SMAPI framework that adds **Familiars** as a new entity class to Stardew Valley. Familiars are distinct from pets, trinkets, and NPCs — they have their own stat system, dialogue, gift responses, diet types, abilities, and a dedicated farm building to live in.

FAUNA provides the engine. Content packs bring the creatures.

---

## Requirements

- [SMAPI](https://smapi.io/) (latest)
- Stardew Valley (latest)

No Content Patcher dependency. No other frameworks required.

---

## For Players

FAUNA adds two things to your game on its own:

- **The Familiar Den** — a new farm building (built via Robin's shop) where your familiars live
- **The Ouija Board** — a craftable item that acts as a shop to purchase familiars

Install a FAUNA content pack to actually add familiars. On its own, FAUNA won't add any creatures.

---

## For Mod Authors

> If you're looking to create a FAUNA content pack, this section is for you.

A FAUNA content pack is a standard SMAPI content pack. No C# required.

### Pack Structure

```
[FAUNA] YourMod/
├── manifest.json
├── familiars.json
├── shops.json              ← optional
└── assets/
    ├── sprites/
    │   └── YourFamiliar.png
    └── portraits/          ← optional
        └── YourFamiliar.png
```

---

### manifest.json

```json
{
  "Name": "Your Mod Name",
  "Author": "YourName",
  "Version": "1.0.0",
  "Description": "A FAUNA content pack.",
  "UniqueID": "YourName.YourModName",
  "ContentPackFor": {
    "UniqueID": "CodysOasis.FAUNA",
    "MinimumVersion": "1.0.0"
  }
}
```

---

### familiars.json

Defines one or more familiar species. Each entry is a `FamiliarData` object.

```json
[
  {
    "FamiliarId": "YourName.YourMod_FamiliarName",
    "DisplayName": "Familiar Name",
    "Description": "Shown in the Familiar Den UI.",
    "FamiliarType": "Foraging",

    "ExcludeFromDefaultShop": false,
    "DefaultShopPrice": 2000,

    "AnimalSprite": "assets/sprites/FamiliarName.png",
    "AnimalPortrait": "assets/portraits/FamiliarName.png",
    "AnimalDialogue": "assets/dialogue/FamiliarName.json",
    "Speaks": true,
    "AlwaysAnimate": false,

    "Diet": "Omnivore",

    "Needs": {
      "FoodDecayPerDay": 0.2,
      "AttentionDecayPerDay": 0.25
    },

    "LovedItems": ["(O)724"],
    "HatedItems": ["(O)262"],

    "Assistance": {
      "BuffIcon": "assets/icons/FamiliarName.png",
      "InventorySize": 0,
      "AllowDeposit": false,
      "Abilities": [
        [],
        [],
        [
          {
            "Id": "YourName.YourMod_FamiliarName_Ability1",
            "AbilityClass": "ForageHarvest",
            "Description": "i18n:FamiliarName_Ability1_Description",
            "Proc": "OnNearby",
            "ProcTimer": 60.0,
            "Condition": "",
            "ProcSound": "",
            "ProcAnimation": false,
            "Args": {}
          }
        ]
      ]
    },

  }
]
```

---

#### FamiliarData Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `FamiliarId` | string | *(required)* | Unique ID. Use `AuthorName.ModName_FamiliarName` format. |
| `DisplayName` | string | *(required)* | Name shown in-game. |
| `Description` | string | `""` | Short text shown in the Familiar Den UI. |
| `FamiliarType` | string | `""` | Category label. Informal — e.g. `"Combat"`, `"Foraging"`, `"Farming"`, `"Social"`. |
| `ExcludeFromDefaultShop` | bool | `false` | If `true`, this familiar won't appear in the Ouija Board shop. |
| `DefaultShopPrice` | int | `5000` | Price in the Ouija Board shop. Ignored if `ExcludeFromDefaultShop` is `true`. |
| `AnimalSprite` | string | `""` | Path to sprite sheet PNG, relative to your mod folder. |
| `AnimalPortrait` | string | `""` | Path to portrait sheet PNG. Optional — omit for no portrait. |
| `AnimalDialogue` | string | `""` | Path to dialogue JSON file. Required if `Speaks` is `true`. |
| `Speaks` | bool | `false` | If `true`, the familiar can be talked to and will show dialogue. |
| `AlwaysAnimate` | bool | `false` | If `true`, the familiar animates even when idle. Use for flying familiars. |
| `Diet` | DietType | `Omnivore` | One of: `Carnivore`, `Herbivore`, `Omnivore`. Controls what food the familiar accepts. |
| `Needs` | NeedsData | *(see below)* | Daily stat decay rates. |
| `LovedItems` | string[] | `[]` | Item IDs the familiar loves as gifts. |
| `HatedItems` | string[] | `[]` | Item IDs the familiar hates as gifts. |
| `Assistance` | AssistanceData | *(see below)* | Ability configuration. |

> **Not yet functional:** `HasHumanoid`, `HumanoidSprite`, `HumanoidPortrait`, `HumanoidDialogue` — humanoid form support is defined in the schema but not yet implemented. These fields can be included for forward compatibility but will have no effect. `DropsLoot` and `LootPool` are also reserved for a future update.

---

#### NeedsData

Controls how fast each need decays per in-game day. Values are on a `0.0`–`1.0` scale.

| Field | Default | Description |
|-------|---------|-------------|
| `FoodDecayPerDay` | `0.2` | How much the familiar's Food need drops each day. |
| `AttentionDecayPerDay` | `0.25` | How much the Attention need drops each day. |

Familiar mood is derived from the average of their current needs. Trust is a long-term stat that grows through consistent care — but it will slowly decay if the familiar is neglected for extended periods.

---

#### Diet Types

| Value | Accepts |
|-------|---------|
| `Carnivore` | Meat, fish, animal products |
| `Herbivore` | Fruits, vegetables, forage |
| `Omnivore` | All food items |

---

#### Gift Tastes

Use standard Stardew Valley item IDs (e.g. `"(O)724"` for Fried Egg). See the [Stardew Valley Wiki](https://stardewvalleywiki.com/Modding:Item_queries) for item ID reference.

FAUNA supports `LovedItems` and `HatedItems`. Any item not listed in either defaults to a **Neutral** response. Additional tiers may be added in a future update.

---

#### AssistanceData

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `BuffIcon` | string | `""` | Icon shown in the buff bar while the familiar is following. Falls back to the FAUNA default icon. |
| `InventorySize` | int | `0` | Number of inventory slots the familiar has. `0` = no inventory. |
| `AllowDeposit` | bool | `false` | If `true`, the player can place items into the familiar's inventory. |
| `Abilities` | List\<List\<AbilityData\>\> | `[]` | Abilities organized by trust tier. See below. |

**Ability Tiers**

Abilities are a positional list of lists — the index corresponds to the trust tier. Abilities within each tier all run simultaneously.

| Index | Hearts |
|-------|--------|
| `0` | 0 hearts |
| `1` | 2 hearts |
| `2` | 4 hearts |
| `3` | 6 hearts |
| `4` | 8 hearts |
| `5` | 10 hearts |

Pass `[]` for a tier with no new abilities. You only need to include entries up to your highest defined tier — you don't need to define all 6. If the player's trust exceeds the highest defined tier, FAUNA falls back to that highest tier.

---

#### AbilityData

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Id` | string | *(required)* | Unique ID for this ability. Use `AuthorName.ModName_FamiliarName_AbilityName`. |
| `AbilityClass` | string | `"Nop"` | What the ability does. See ability classes below. |
| `Description` | string | `""` | UI description. Use an i18n key (e.g. `"i18n:MyAbility_Description"`). |
| `Proc` | string | `"OnFollow"` | When the ability activates. `"OnFollow"` (timed, while following) or `"OnNearby"` (when a target is in range). |
| `ProcTimer` | float | `60.0` | Cooldown in real-time seconds between procs. `-1` = no cooldown. |
| `Condition` | string | `""` | Optional GSQ condition that must pass before the ability procs. |
| `ProcSound` | string | `""` | Sound cue to play on proc. |
| `ProcAnimation` | bool | `false` | If `true`, plays the Row 10 proc animation (frames 36–39) on proc. |
| `Args` | Dictionary\<string, string\> | `{}` | Ability-class-specific arguments. See below. |

**Ability Classes**

| AbilityClass | Description | Args |
|--------------|-------------|------|
| `LuckBuff` | Passive luck bonus | `"Magnitude": "1"` |
| `SpeedBuff` | Passive speed bonus | `"Magnitude": "1"` |
| `Heal` | Restores player health | `"Amount": "10"` |
| `Energy` | Restores player energy | `"Amount": "10"` |
| `AnimalFriendship` | Boosts friendship with nearby farm animals | `"Range": "256"`, `"Amount": "15"` |
| `NPCFriendship` | Boosts friendship with nearby NPCs | `"Range": "256"`, `"Amount": "15"` |
| `FamiliarFriendship` | Boosts friendship with nearby familiars | `"Range": "256"`, `"Amount": "15"` |
| `Melee` | Attacks nearby monsters | `"Range": "128"`, `"Damage": "5"` |
| `Fisher` | Assists with fishing | *(none required)* |
| `CropHarvest` | Harvests nearby mature crops | *(none required)* |
| `AnimalHarvest` | Collects products from nearby farm animals | *(none required)* |
| `ForageHarvest` | Collects nearby forage items | *(none required)* |
| `Nop` | Does nothing. Useful as a placeholder. | *(none)* |

---

### shops.json (Optional)

Adds a custom familiar shop. If omitted, your familiars are only sold through the Ouija Board (unless `ExcludeFromDefaultShop` is set).

```json
[
  {
    "ShopId": "YourName.YourMod_ShopName",
    "DisplayName": "Shop Display Name",
    "ShopOwner": "NpcInternalName",
    "ShopLocation": "Custom_LocationName",
    "UseDefaultAccess": true,
    "Stock": [
      {
        "FamiliarId": "YourName.YourMod_FamiliarName",
        "Price": 2000,
        "Condition": "PLAYER_FRIENDSHIP_POINTS Current NpcInternalName 500",
        "RequiredMailFlag": "",
        "Season": "",
        "HiddenUntilUnlocked": false
      }
    ]
  }
]
```

#### FamiliarShopData Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `ShopId` | string | *(required)* | Unique shop ID. |
| `DisplayName` | string | `"Familiar Shop"` | Name shown in the shop menu. |
| `ShopOwner` | string | `""` | Internal NPC name for the shop owner. Optional. |
| `ShopLocation` | string | `""` | Location name for the shop. Optional. |
| `UseDefaultAccess` | bool | `true` | If `true`, uses the framework's default shop access logic. |
| `Stock` | FamiliarShopEntry[] | `[]` | Familiars sold in this shop. |

#### FamiliarShopEntry Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `FamiliarId` | string | *(required)* | The familiar species sold. |
| `Price` | int | `5000` | Purchase price in gold. |
| `Condition` | string | `""` | GSQ condition that must pass for this entry to appear. |
| `RequiredMailFlag` | string | `""` | Mail flag that must be set for this entry to appear. |
| `Season` | string | `""` | Restrict to a specific season (`"spring"`, `"summer"`, `"fall"`, `"winter"`). Empty = always available. |
| `HiddenUntilUnlocked` | bool | `false` | If `true`, the entry is hidden in the shop until its condition is met. |

`Condition` accepts standard [Game State Queries](https://stardewvalleywiki.com/Modding:Game_state_queries). Example from Vael's Familiars:
```
"PLAYER_FRIENDSHIP_POINTS Current CodysOasis.TotAG_CP_AetherGlade_Vael 500"
```

---

### Sprite Sheet Requirements

**Animal sprites** (`assets/sprites/`)
- PNG, transparent background
- Standard Stardew scale: **16×16 px** per frame
- Frames laid out horizontally per animation row
- Row layout:

| Row | Content |
|-----|---------|
| 1 | Walking Down |
| 2 | Walking Right |
| 3 | Walking Up |
| 4 | Walking Left |
| 5–9 | Reserved for future use (emotes, sleep animations, etc.) |
| 10 | Proc animation — used when `ProcAnimation: true` |

**Portraits** (`assets/portraits/`) — optional
- PNG, transparent background
- Standard Stardew portrait size: **64×64 px** per frame
- Only shown if `Speaks: true` and `AnimalPortrait` is set

---

### i18n

Ability descriptions support i18n keys. Define them in your pack's `i18n/default.json`:

```json
{
  "MyFamiliar_Ability1_Description": "Collects nearby forage while following you."
}
```

Reference in `familiars.json` as `"i18n:MyFamiliar_Ability1_Description"`.

---

## Example Packs

- **[Vael's Familiars](https://www.nexusmods.com/stardewvalley)** — the official FAUNA content pack, part of the Tales of the Aether Glade series. Adds 11 familiars across multiple species, gated behind friendship with Vael.

---

## Known Mods Using FAUNA

*Making a FAUNA content pack? Get in touch to be listed here — message **CodysOasis** on [Nexus Mods](https://www.nexusmods.com) or on Discord at **@codysoasis**.*

---

## Compatibility

- Compatible with Content Patcher mods
- Compatible with SMAPI mods that don't override farm building logic
- Familiars are a distinct entity class — mods targeting pets, trinkets, or NPCs specifically will not affect them

---

## License

MIT — contributions welcome.

---

## Credits

**FAUNA** by CodysOasis
