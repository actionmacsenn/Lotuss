# Lotus

A low-poly Unity horde game where you are a giant pink lotus rooted in the middle of a forest clearing.
Endless waves of blight creatures pour out of the treeline, and you tear them apart with plant powers:
vines that drag enemies into the earth, petal tornadoes, giant flytraps, sunbeams, exploding mushrooms and more.

Everything is generated in code at runtime: the meshes, world, particles, UI, and every sound and music track.
There are no model, texture or audio files.

## Play

Download `Lotus-Windows.zip` from the **Releases** page, unzip it, and run `Lotus.exe`.

## Controls

| Input | Action |
|---|---|
| Left mouse | Cast the selected ability at the cursor |
| Hold right mouse | Open the ability wheel (time slows) |
| Release right mouse on a slice | Select that ability |
| Wheel centre / scroll / Tab | Next page of abilities |
| 1 - 6 | Quick select on the current page |
| M | Toggle music |
| Esc | Back to the menu |

## Features

- **12 plant abilities** over two wheel pages: Root Abyss, Bramble Rupture, Vine Lash, Petal Cyclone, Snapjaw,
  Seed Barrage, Pollen Haze, Strangleroot, Solar Lance, Sporecap Field, Elder Sapling and Grand Bloom.
- **Waves.** Each wave streams a set number of enemies. When 10 or fewer are left they glow pink with a marker.
- **Flowers.** You earn this currency from every kill and from wave-clear bonuses.
- **Upgrades.** Each ability has 3 upgrade tracks (36 in all), bought with flowers from the menu.
- **Bramble pit.** A thorny ring around the lotus finishes off anything that walks in.
- **Procedural audio.** 45 synthesized sound effects, forest ambience, a calm lo-fi menu beat and battle music.
- **Settings.** Separate Master, Music, Effects and Ambience volume sliders.

## Open in Unity

1. Install **Unity 6000.6.4f1** (Unity 6) with Windows Build Support.
2. In Unity Hub, choose **Add > Add project from disk** and select this folder.
3. Open `Assets/Scenes/Lotus.unity` and press **Play**. The game builds itself from code at startup.
4. To make an exe, use the **Lotus > Build Windows** menu. The output goes to `Builds/Windows/`.

## Code map

| Folder | What's inside |
|---|---|
| `Assets/Scripts/Core` | Game flow, camera, particles, procedural audio (`SFX.cs`), save data, low-poly mesh toolkit (`LP.cs`) |
| `Assets/Scripts/World` | Forest, field, bramble pit, death flowers |
| `Assets/Scripts/Player` | The lotus and the vine tube renderer |
| `Assets/Scripts/Enemies` | Enemy behaviour and the wave spawner |
| `Assets/Scripts/Abilities` | Ability and upgrade definitions, casting, and every effect |
| `Assets/Scripts/UI` | HUD, ability wheel, menu, upgrades shop, settings |
| `Assets/Resources/Shaders` | Flat-shaded vertex-colour, glow and particle shaders |
