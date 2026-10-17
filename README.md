# Forge of Potential Reworked

A Valheim mod (BepInEx) that reworks the Forge of Potential.

In the game, each Forge upgrade is a 65% gamble, and a failure destroys the item. A character's first roll for each item and level depends only on the character's name, so the best way to use the Forge is to make throwaway characters with lucky names (see `upgrade_oracle.py` in [ValheimSaveParser](https://github.com/Melichor/ValheimSaveParser)). This mod replaces the gamble with something you earn by playing.

## How it works

Every item the Forge of Potential can refine (weapons, armour, shields and tools) gathers experience as you use it. The experience is stored on the item itself, so it stays when the item is saved, put in a chest, dropped or given to another player. Hover over the item to see it, and a bar above the durability bar fills up in the inventory. When the bar is full it turns gold and the game tells you the item is ready.

Take it to the Forge of Potential and refine it. There is no roll and nothing breaks: the item goes up one level, is fully repaired and keeps its crafter's name. The bar starts again from empty, and **experience never carries over**: once the bar is full, the item gains nothing more until you refine it. Refining is a step you choose, not something you farm towards the maximum.

| Item | Earns experience from |
|---|---|
| Weapons | Damage dealt to creatures, after their resistances and without overkill, including burning and poison. The killing blow gives extra experience. |
| Armour (helmet, chest, legs, cape) | Every hit from a creature, taken or blocked. Every worn piece gets it. |
| Shields | Damage blocked. A parry gives twice as much. |
| Pickaxes and axes | Mining rocks and ore, chopping trees and logs, when the tool is strong enough to damage them. Axes also earn as weapons. |
| Hammer, hoe, cultivator, serving tray | Each piece, plant or terrain change placed, and each repair. After you remove a piece the next placement gives nothing, as with the game's build skill, so placing and removing does not farm it. |
| Scythe, snow shovel | Each plant harvested, each shovelful of snow (Deep North). |
| Staffs that deal no damage (summoning, shielding) | Each cast, by its eitr or health cost. |

Hits from players, tamed creatures, summons and training dummies give no experience, and neither do falls, fire on the ground or drowning.

The highest level is 25. A refinement takes idols of the item's tier: the new level divided by 5, rounded up.

The experience an item needs grows with its level and with its tier. Experience comes from creatures' health, and a typical kill in the Ashlands gives about ten times what one in the Meadows does, so an item of an earlier tier needs less. The tier multipliers follow how much experience a typical kill in that biome gives, measured from two characters' kills: refining an item with the creatures of its own biome takes about as many kills at every tier, roughly 3,000 to 4,000 kills from level 4 to 25. Taken into a later biome, where kills give more, an item levels faster than new items of that biome. Old favourites catch up, but a new item of the later tier still ends higher.

| Tier (idol) | Multiplier | 4 to 5 | 24 to 25 | 4 to 25 |
|---|---|---|---|---|
| 0 Meadows | 0.15 | 1,200 | 17,636 | 176,861 |
| 1 Black Forest | 0.4 | 3,200 | 47,030 | 471,630 |
| 2 Swamp | 0.45 | 3,600 | 52,909 | 530,584 |
| 3 Mountain | 0.55 | 4,400 | 64,667 | 648,491 |
| 4 Plains | 0.85 | 6,800 | 99,940 | 1,002,214 |
| 5 Mistlands | 0.9 | 7,200 | 105,818 | 1,061,168 |
| 6 Ashlands | 1.6 | 12,800 | 188,122 | 1,886,520 |
| 7 Deep North | 4 | 32,000 | 470,304 | 4,716,300 |

An item's tier is the tier of the idol its recipe takes. A few recipes take idols two or more tiers away from where the item is crafted (the Nord Atgeir takes bronze idols); for those the experience follows the crafting biome (`TierCorrections` in `ItemXp.cs`), while the idols stay the game's. From level 4 (the usual maximum at a workbench or forge) to 25 takes 71 idols. Upgrading at a normal station, with materials, still works as before. It makes a new item, so it also empties the bar.

### Settings

`BepInEx/config/melichor.ValheimFOPReworked.cfg` is created on the first start. The experience rates are first guesses that need playing to tune.

| Setting | Default | Meaning |
|---|---|---|
| `MaxLevel` | 25 | The highest level the Forge refines to. |
| `LevelsPerIdol` | 5 | Idols per refinement: the new level divided by this, rounded up. |
| `XpBase`, `XpExponent` | 1000, 1.5 | Experience needed at level L: `XpBase * tier multiplier * L ^ XpExponent`. |
| `TierXpScale` | 0.15, 0.4, 0.45, 0.55, 0.85, 0.9, 1.6, 4 | The tier multipliers, for tiers 0 (Meadows) to 7 (Deep North). |
| `DamageXp` | 1 | Weapons, per point of damage. |
| `KillXp` | 0.5 | Weapons, extra for a kill, per point of the creature's maximum health. |
| `ArmorXp` | 2 | Armour, per point of a creature's hit. |
| `BlockXp`, `ParryMultiplier` | 2, 2 | Shields, per point blocked, and the multiplier for a parry. |
| `ResourceXp` | 1 | Pickaxes and axes, per point of pickaxe or chop damage. |
| `ToolUseXp` | 20 | Build tools, scythe, snow shovel, per use, times the tool's tier multiplier, so every tool needs the same number of uses. |
| `CastXp` | 2 | Staffs that deal no damage, per point of eitr or health a cast costs. |

### Multiplayer

Everyone should have the mod. A creature is simulated on one player's machine, and that machine works out the damage and tells the attacker. Without the mod there, hits on its creatures give no weapon experience. The settings are read on each machine and are not sent from the server.

### Testing

With `devcommands` on, the console command `fopxp` fills the bar of every equipped item (`fopxp clear` empties it, `fopxp 500` adds 500). The `nocost` cheat and the "no crafting cost" world setting refine without experience or idols.

## Setup

You need Python 3, the .NET 10 SDK and Valheim from Steam. The setup and the build are tested on Linux with the native Linux game.

```bash
./setup.sh
```

It finds Valheim in your Steam libraries and gets everything else the build needs. Everything it downloads or generates goes into `deps/`, and the game path goes into `Environment.props`. Git ignores both. Running it again only redoes what is missing or out of date: a new pinned version, or a game update for the decompiled code.

| What | Where |
|---|---|
| BepInExPack_Valheim (pinned version, checked against its sha256), for building and for installing into the game | `deps/BepInExPack_Valheim/` |
| ilspycmd, the ILSpy decompiler | `deps/tools/` |
| The game's code (`assembly_valheim.dll`, `assembly_utils.dll`), decompiled to read and search | `deps/decompiled/` |
| The path of your Valheim install | `Environment.props` |

| Option | Meaning |
|---|---|
| `--game PATH` | Use this Valheim install (the folder that contains `valheim_Data`). `VALHEIM_INSTALL` works too. Without it, the last path used is kept, then the Steam libraries are searched. |
| `--no-decompile` | Skip ilspycmd and the decompiled code. |
| `--install-bepinex` | Also copy BepInEx into the game folder. This is needed to run the mod. An existing `BepInEx/config/BepInEx.cfg` is kept. |

`setup.sh` only runs `tools/setup.py`, which you can also run directly with Python.

## Build

```bash
dotnet build
```

The plugin is compiled against the game's own assemblies, from your install. The private members of `assembly_valheim` and `assembly_utils` are made public at compile time (BepInEx.AssemblyPublicizer), so patches can use them directly.

## Running the mod

1. Install BepInEx into the game, once: `./setup.sh --install-bepinex`
2. In Steam, set the game's launch options (Properties > General) to `./start_game_bepinex.sh %command%`
3. Build and copy the plugin into `BepInEx/plugins/ValheimFOPReworked/`: `dotnet build -p:Deploy=true`

BepInEx writes its log to `BepInEx/LogOutput.log` in the game folder.

## Checking the patches

Harmony only finds a patch that no longer fits the game (a renamed method or argument) when the game loads the plugin. `tools/PatchCheck` finds it without starting the game. It reads the built plugin and the installed game as metadata and checks every patch against the method it patches. Run it after a game update:

```bash
dotnet build && dotnet run --project tools/PatchCheck
```

## Layout

| Path | What |
|---|---|
| `src/ValheimFOPReworked/` | The plugin |
| `setup.sh`, `tools/setup.py` | The setup script |
| `tools/PatchCheck/` | Checks the patches against the installed game |
| `deps/`, `Environment.props` | Made by the setup, not committed |

## Notes on the game code

- The Forge's upgrade happens in `InventoryGui.DoCrafting`: the recipe's idol is the requirement marked `m_upgraderResource`, `Player.GetItemUpgradeChance` rolls against the idol's `m_upgradeChance`, then `m_breakChance` decides between breaking the item and lowering its level. The mod replaces that branch (`Forge.Refine`) and changes the idol count in `Piece.Requirement.GetAmount`, which every check, display and consumption of upgrader requirements goes through.
- The game's upgrades remove the old item and add a new one (`Inventory.AddItem`), so anything stored on the old item is lost. The mod's refinement changes the item in place instead.
- `ItemDrop.ItemData.m_customData` (string to string) is saved and sent over the network with the item, wherever it is. The experience lives there, under `melichor.ValheimFOPReworked.xp`.
- `Character.Damage` runs on the attacker and only sends the hit to the creature's owner, where `RPC_Damage` and `ApplyDamage` work out the damage. Burning and poison damage (`SE_Burning`, `SE_Poison`) has no attacker at all.
- A thrown spear keeps its experience from the hit only when the creature is simulated on your own machine. The report from another machine arrives after the spear has left your inventory.
