# Smart Recipe Restock

[日本語](README.ja.md)

Dalamud plugin that reads the open crafting recipe and withdraws missing materials from each retainer.

**Something Need Doing (SND) is not required.**

## Install

1. Run `/xlsettings` and open the **Experimental** tab
2. Add this URL under **Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/Qmeko/DalamudPlugins/refs/heads/main/pluginmaster.json
```

3. Run `/xlplugins` and install **Smart Recipe Restock**

## How to use

1. Open the Recipe Note and select a recipe
2. Type `/srr` in chat
3. Click **Read recipe**
4. Open the retainer list at a summoning bell
5. Check **Allow full-stack withdraw**
6. Click **Withdraw from all retainers**

## Notes

- The game withdraws a **full stack**, not an exact count
- Crystals (item IDs 2–19) are skipped
- You must open the retainer list yourself
- With [Allagan Tools](https://github.com/Critical-Impact/InventoryTools) installed, only retainers that have the missing items are opened. Without it, every retainer is checked in order

## Commands

| Command | Description |
| --- | --- |
| `/srr` | Open / close the window |
| `/smartreciperestock` | Same |

## For developers

```powershell
.\install-dev.ps1
```

This copies the plugin to:

```text
%APPDATA%\XIVLauncher\devPlugins\SmartRecipeRestockHelper\
```
