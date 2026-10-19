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

An item's tier is the tier of the idol its recipe takes. In the game, a few recipes take idols of another tier than where the item is crafted; most look copied from an earlier recipe. The mod gives these items the idols of their crafting biome, of the same kind (battle or protection), and the experience follows that tier too (`IdolFixes.cs`):

| Item | Idol tier in the game | With the mod |
|---|---|---|
| Nord Atgeir | 1 Black Forest | 7 Deep North |
| Nord Knucklechains | 2 Swamp | 7 Deep North |
| Nord Axe, Frostfire Axe | 4 Plains | 7 Deep North |
| Headdress of the Bear | 5 Mistlands | 1 Black Forest |
| Early Axes | 2 Swamp | 0 Meadows |
| Splitnir, and the Bleeding, Storming and Primal variants | 5 Mistlands | 6 Ashlands |

The Scythe, Snow Shovel and Fishing Hat are off too (crafted in the Mountain, Ashlands and Deep North), but keep the game's Meadows and Black Forest idols: they are tools more than gear.

The Helmet of the Protector has levels but no idol in the game, so the Forge never lists it, unlike the rest of its set. The mod gives it the same Bloodgold Protection idol as its breastplate and trousers.

From level 4 (the usual maximum at a workbench or forge) to 25 takes 71 idols. Upgrading at a normal station, with materials, still works as before. It makes a new item, so it also empties the bar.

### Crafting idols

Besides finding them, idols can be crafted from the tier's boss item, a trophy of the next tier and 5 of the material the idol is named after, at the best station that tier reaches just after its boss. Bloodgold has no next tier, so it takes a rare Ashlands trophy (they drop 5% of the time). A recipe shows up once you have picked up all its ingredients and seen the station at that level. Each craft takes one trophy and makes one idol (`IdolRecipes.cs`).

| Idol | Station | Boss item per idol (per kill) | Material | Battle idol | Protection idol |
|---|---|---|---|---|---|
| Wooden | Workbench 3 | 1 Hard Antler (3) | 5 Fine Wood | Troll Trophy | Greydwarf Shaman Trophy |
| Bronze | Forge 3 | 1 Swamp Key (1 per player) | 5 Bronze | Draugr Elite Trophy | Abomination Trophy |
| Iron | Forge 7 | 1 Wishbone (1 per player) | 5 Iron | Drake Trophy | Wolf Trophy |
| Silver | Artisan Table 1 | 5 Dragon Tears (10) | 5 Silver | Deathsquito Trophy | Lox Trophy |
| Black Metal | Forge 7 | 1 Torn Spirit (3) | 5 Black Metal | Gjall Trophy | Hare Trophy |
| Black Marble | Black Forge 3 | 2 Majestic Carapaces (5) | 5 Black Marble | Bonemaw Trophy | Asksvin Trophy |
| Flametal | Black Forge 5 | 2 Kindled Ribs (5) | 5 Flametal | Barka Trophy | Krigen Trophy |
| Bloodgold | Black Forge 6 | 1 Crown Jewel (1) | 5 Bloodgold | Fallen Valkyrie Trophy | Morgen Trophy |

### Settings

`BepInEx/config/melichor.ValheimFOPReworked.cfg` is created on the first start. Edits to the file apply while the game runs. The experience rates are first guesses that need playing to tune.

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

All experience earned by playing is also multiplied by the world's resource rate (the "Resources" world setting, which scales drops), so a world set to more resources levels items faster too.

### Multiplayer

Everyone should have the mod, in the same version. A creature is simulated on one player's machine, and that machine works out the damage and tells the attacker. Without the mod there, hits on its creatures give no weapon experience.

The server's settings apply to everyone: it sends them as it lets a player in, and again whenever they change, so editing the server's config file takes effect without a restart. A player's own config file applies again in single player and when they host.

### Existing items

Items refined above level 25 before the mod (with the game's gamble) keep their level. They gain no experience and cannot be refined further.

### Compatibility

Other Forge of Potential mods that change the refinement roll do not work with this one, which replaces the roll altogether. BepInEx does not load this mod next to these: Potential Reforged (Dyls123), JGFoP (JGSTUDIOS), OdinBet Forge of Potential (Igao), ForgeRework (MerlinofMines), Reforged Potential (Akuichi), MySix Forge of Potential (MysixValheimMods), Forge of Potential Redux (Dreich), Wire's Forge of Goodtential (Wire), Forge Odds (Sansara), Forge of True Potential (beijing_carlo) and Potential Draught (LOSDC). Any other mod that patches the refinement is named in a warning in `BepInEx/LogOutput.log` at start.

Mods that only add ways to get idols (from bosses, enemies, trophies, or by downgrading them) work alongside it. Sure Potential (TeflonTed) can stay installed too: it only makes the game's roll always succeed, and this mod has no roll.

### Translations

The mod's text is in `src/ValheimFOPReworked/Translations/<Language>.json`, built into the plugin. To add a language, copy `English.json` to the game's name for it (`Czech.json`, `German.json`, ...) and translate the values; the game fills in `$1` and `$2`. Words missing from a language show in English.

### Testing

With `devcommands` on, the console command `fopxp` fills the bar of every equipped item (`fopxp clear` empties it, `fopxp 500` adds 500). The `nocost` cheat and the "no crafting cost" world setting refine without experience or idols.

## Requirements

To build the mod you need:

| What | Why | Where to get it |
|---|---|---|
| Valheim, installed through Steam | The mod is compiled against the game's own files, so the game has to be on the machine that builds it. | Steam |
| The .NET 10 SDK (`dotnet`) | Builds the mod, and runs the decompiler and the patch check. | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0). Windows: `winget install Microsoft.DotNet.SDK.10`. Linux: your distribution's `dotnet-sdk-10.0` package, or Microsoft's package repository. |
| Python 3.8 or newer | Runs the setup and build scripts. | Windows: [python.org](https://www.python.org/downloads/), whose installer also adds the `py` launcher the scripts use. Linux: usually installed already, otherwise the `python3` package. |
| An internet connection, the first time | The setup downloads BepInEx (from Thunderstore) and the ILSpy decompiler, and the first build downloads two NuGet packages. | |

Nothing else: no Visual Studio, Mono or Unity. Git is only needed to get the code.

The scripts work on Linux and Windows. They are tested on Linux with the native Linux game; the Windows scripts (`setup.bat`, `build.bat`) do the same as the `.sh` ones but have not been run yet.

## Setup

```bash
./setup.sh
```

On Windows, run `setup.bat` instead; it takes the same options.

It finds Valheim in your Steam libraries and gets everything else the build needs. Everything it downloads or generates goes into `deps/`, and the game path goes into `Environment.props`. Git ignores both. Running it again only redoes what is missing or out of date: a new pinned version, or a game update for the decompiled code.

| What | Where |
|---|---|
| BepInExPack_Valheim (pinned version, checked against its sha256), for building and for installing into the game | `deps/BepInExPack_Valheim/` |
| ilspycmd, the ILSpy decompiler | `deps/tools/` |
| The game's code (`assembly_valheim.dll`, `assembly_utils.dll`, `assembly_guiutils.dll`), decompiled to read and search | `deps/decompiled/` |
| The path of your Valheim install | `Environment.props` |

| Option | Meaning |
|---|---|
| `--game PATH` | Use this Valheim install (the folder that contains `valheim_Data`). `VALHEIM_INSTALL` works too. Without it, the last path used is kept, then the Steam libraries are searched. |
| `--no-decompile` | Skip ilspycmd and the decompiled code. |
| `--install-bepinex` | Also copy BepInEx into the game folder. This is needed to run the mod. An existing `BepInEx/config/BepInEx.cfg` is kept. |

`setup.sh` and `setup.bat` only run `tools/setup.py`, which you can also run directly with Python.

## Build

```bash
./build.sh
```

On Windows, run `build.bat` instead; it takes the same options. It builds the plugin (`src/ValheimFOPReworked/bin/Release/netstandard2.1/ValheimFOPReworked.dll`) and then checks its patches against the installed game (see [Checking the patches](#checking-the-patches)). It stops with an error if either fails.

| Option | Meaning |
|---|---|
| `--debug` | Build the Debug configuration instead of Release. |
| `--deploy` | Also copy the plugin into the game's `BepInEx/plugins/ValheimFOPReworked/`. BepInEx has to be installed in the game (`./setup.sh --install-bepinex`). |
| `--no-check` | Skip the patch check. |
| `--package` | Also make `dist/ForgeOfPotentialReworked-<version>.zip`, ready to install (see below). |

`build.sh` and `build.bat` only run `tools/build.py`, which runs `dotnet build`; plain `dotnet build` works too. The plugin is compiled against the game's own assemblies, from your install. The private members of `assembly_valheim`, `assembly_utils` and `assembly_guiutils` are made public at compile time (BepInEx.AssemblyPublicizer), so patches can use them directly.

### The package

`./build.sh --package` makes the zip that Thunderstore and mod managers (r2modman, Thunderstore Mod Manager, Gale) install, in Thunderstore's layout:

| File | From |
|---|---|
| `manifest.json` | The name, description and website in `tools/build.py`, the version from `Plugin.Version` in `Plugin.cs`, and BepInExPack_Valheim as a dependency (the version `setup.py` pins) |
| `README.md` | This README from the top to just before "Requirements", the part for players |
| `icon.png` | `package/icon.png`, 256x256, exported from `package/icon.xcf`, the icon's source. Edit the `.xcf` in GIMP (each part is its own layer), then run `package/export-icon.sh`, which exports it with GIMP. The current icon is a placeholder. |
| `CHANGELOG.md` | `package/CHANGELOG.md`, if there is one |
| `plugins/ValheimFOPReworked.dll` | The Release build |

To release a new version, raise `Plugin.Version` (three numbers, like 0.2.0) and package again. To install the zip without a mod manager, copy `plugins/ValheimFOPReworked.dll` into the game's `BepInEx/plugins/ValheimFOPReworked/`.

## Running the mod

1. Install BepInEx into the game, once: `./setup.sh --install-bepinex` (Windows: `setup.bat --install-bepinex`)
2. On Linux only: in Steam, set the game's launch options (Properties > General) to `./start_game_bepinex.sh %command%`. On Windows, BepInEx starts with the game by itself.
3. Build and copy the plugin into `BepInEx/plugins/ValheimFOPReworked/`: `./build.sh --deploy` (Windows: `build.bat --deploy`)

BepInEx writes its log to `BepInEx/LogOutput.log` in the game folder.

## Checking the patches

Harmony only finds a patch that no longer fits the game (a renamed method or argument) when the game loads the plugin. `tools/PatchCheck` finds it without starting the game. It reads the built plugin and the installed game as metadata and checks every patch against the method it patches. `./build.sh` runs it after every build, so after a game update a build is enough. On its own:

```bash
dotnet run --project tools/PatchCheck
```

## Layout

| Path | What |
|---|---|
| `src/ValheimFOPReworked/` | The plugin |
| `setup.sh`, `setup.bat`, `tools/setup.py` | The setup script |
| `build.sh`, `build.bat`, `tools/build.py` | The build script |
| `package/` | The package's icon (`icon.xcf` is its source, `export-icon.sh` exports it) and changelog |
| `tools/PatchCheck/` | Checks the patches against the installed game |
| `src/ValheimFOPReworked/Translations/` | The mod's text, per language |
| `deps/`, `Environment.props` | Made by the setup, not committed |
| `dist/` | Made by `--package`, not committed |

## Notes on the game code

- The Forge's upgrade happens in `InventoryGui.DoCrafting`: the recipe's idol is the requirement marked `m_upgraderResource`, `Player.GetItemUpgradeChance` rolls against the idol's `m_upgradeChance`, then `m_breakChance` decides between breaking the item and lowering its level. The mod replaces that branch (`Forge.Refine`) and changes the idol count in `Piece.Requirement.GetAmount`, which every check, display and consumption of upgrader requirements goes through.
- The game's upgrades remove the old item and add a new one (`Inventory.AddItem`), so anything stored on the old item is lost. The mod's refinement changes the item in place instead.
- `ItemDrop.ItemData.m_customData` (string to string) is saved and sent over the network with the item, wherever it is. The experience lives there, under `melichor.ValheimFOPReworked.xp`.
- `Character.Damage` runs on the attacker and only sends the hit to the creature's owner, where `RPC_Damage` and `ApplyDamage` work out the damage. Burning and poison damage (`SE_Burning`, `SE_Poison`) has no attacker at all.
- A thrown spear keeps its experience from the hit only when the creature is simulated on your own machine. The report from another machine arrives after the spear has left your inventory.
