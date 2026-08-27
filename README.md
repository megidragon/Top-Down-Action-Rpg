# 🏹 El Tesoro del Bosque

*A top-down Action RPG roguelike made in Unity 6 with the [Tiny Swords](https://pixelfrog-assets.itch.io/tiny-swords) asset pack.*

Legend has it that an unclaimed treasure sleeps in the heart of the kingdom's most dangerous forest. Many went in to find it. None came back. Pick your class, cross the wall of trees and fight your way through **10 increasingly hostile levels** to the heart of the forest.

![Title screen](Screenshots/titulo.png)

## ✨ Features

- **4 playable classes**, each with its own identity and kit
- **Real-time action combat** with directional parry, dash and energy management
- **Roguelike**: 10 forest levels with unique layouts, rising difficulty and permadeath
- **Rest camps** with a healing campfire and merchants selling potions and elixirs
- **Stats system** (strength / defense / speed) that grows over the run
- Full UI: title menu, settings (resolution, **ES/EN** language, volumes, screen shake) and pause
- Windows installer

## ⚔️ Combat system

![Combat in the forest](Screenshots/combate.png)

Combat revolves around the mouse: **you aim where you look**, and player and enemies play by the same rules.

| Mechanic | How it works |
|---|---|
| **Primary attack** (left click) | Short-range **130°** fan sweep centered on the cursor |
| **Special attack** (right click) | Longer straight-line thrust (or each class's own special) |
| **Parry** (Space) | Blocks **one** hit landing inside a 60° cone toward your aim — the blocked attacker is knocked back and staggered. It blocks arrows too! |
| **Dash** (Shift) | Quick burst in your movement direction |
| **Energy** | Every attack and dash costs 25 out of a self-regenerating 50-point bar: no spamming |
| **Hits** | Landed blows knock the target back and shake the camera (can be disabled in settings) |

### The classes

![Class selection](Screenshots/seleccion-clase.png)

- 🗡️ **Warrior** — the balanced one: 150 HP, a fast and reliable sword.
- 🔱 **Lancer** — superior reach and more damage per hit, but his heavy weapon takes 75% longer to recover between attacks. 112 HP.
- 🏹 **Archer** — the glass cannon: 75 HP. Hold left click to aim an area **arrow rain** (the target zone is marked on the ground half a second before impact) and fire **3-arrow bursts** with right click.
- ✊ **Monk** — the brawler: no parry, but he **heals himself** (and nearby allies) with Space, his kicks send enemies flying, and his **charge** — steerable mid-flight — stuns on impact. 125 HP.

Enemies use all four classes with their own AI: archers telegraph their arrow rain and keep their distance, monks heal and charge, lancers outrange you.

## 🌲 The expedition

![Starting town](Screenshots/ciudad.png)

1. **The town**: your starting point, with its townsfolk at work. The forest entrance is marked to the north.
2. **Levels 1–10**: maps walled in by trees — clearings, ponds, fordable rivers, swamps, grove mazes... Clear the level to unlock the exit. Every 3 levels one more enemy joins the fight.
3. **Camps** (after levels 3, 6 and 9): rest by the campfire (full heal) and buy from the merchants — 1/2/3-use potions (1/3/6 coins) or **permanent elixirs** of strength, defense or speed (4 coins).
4. **The treasure**: your reward awaits on level 10... if you make it.

Enemies drop coins on death and you pick them up just by walking close. The inventory has 4 slots (keys 1–4) and health bottles don't stack: each one takes its own slot with its remaining uses.

## 🎮 Controls

| Action | Control |
|---|---|
| Move | `WASD` |
| Dash | `Shift` |
| Primary / special attack | `Left click` / `Right click` |
| Parry or heal | `Space` |
| Use item | `1`–`4` |
| Interact / buy | `E` |
| Pause | `ESC` |
| Retry after dying | `R` |

## 🛠️ Development

- **Unity 6** (6000.0) with URP 2D and the Input System.
- The whole world is generated **at runtime**: the scene only contains the systems, and the 12 maps (town, 10 levels and the rest camp) are painted on the fly with autotiling over the Tiny Swords tileset.
- The scene is fully regenerated from the **TinyRpg → Construir escena del juego** menu; the executable with **TinyRpg → Compilar juego (Windows)** and the installer with [Inno Setup](https://jrsoftware.org/isinfo.php) (`Installer/ElTesoroDelBosque.iss`).

## 🙏 Credits

- **Art**: [Tiny Swords](https://pixelfrog-assets.itch.io/tiny-swords) by **[Pixel Frog](https://pixelfrog-assets.itch.io/)** — an exceptional asset pack. Thank you!
- **Item icons**: *Tiny Fantasy Icons* by **Vespa Warrior** (Unity Asset Store).

## 📄 License

The project's source code is released under the [MIT](LICENSE) license — use it, learn from it and modify it freely. Art assets belong to their creators and keep their own licenses.
