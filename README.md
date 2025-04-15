# Deal-Optimizer-Mod
A MelonLoader mod that optimizes deals for the Steam game [Schedule I](https://store.steampowered.com/app/3164500/Schedule_I/)

Features:
* Evaluates the current offer and displays its probability of success in the Counteroffer UI
* Displays the price per unit of the current offer in the Counteroffer UI (enabled by default; configurable)
* Displays the maximum daily cash for the customer in the Counteroffer UI (enabled by default; configurable)
* Attempts to set a price for the current quantity that has the highest probability of success during Counteroffers
* Automatically sets the maximum daily cash the customer has during Offers (Street Deals)

## Configuration
> [!TIP]
> When the mod is loaded in-game for the first time, a placeholder configuration file is created for you and can be found at `SteamLibrary\steamapps\common\Schedule I\Mods\DealOptimizer\DealOptimizer_Config.json`

* This configuration file works for both versions of this mod
* To enable a flag, change its value to "true"; to disable a flag, change its value to "false"

| Flags | Description | Default |
| - | - | - |
| PrintCalculationsToConsole | Print calculation steps to the MelonLoader console | Disabled |
| PricePerUnitDisplay | Display the price per unit in the Counteroffer UI | Enabled |
| MaximumDailySpendDisplay | Display the customer's maximum daily spend in the Counteroffer UI | Enabled |

## Installation
> [!NOTE]
> You can select your game version via Steam > Schedule I > Properties > Betas > Beta Participation. By default, it will be `none` (normal version of the game).
> 
> For `none` or `beta`, use the `IL2CPP` version. For `alternate` or `alternate-beta`, use the `Mono` version.
>
> Generally, `Mono` plays nicer with mods, but is less performant compared to `IL2CPP`.

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader) for the game.
2. After installing MelonLoader for the game, run the game once for MelonLoader to setup. Exit the game.
3. Download the [latest release](https://github.com/xyrilyn/Deal-Optimizer-Mod/releases/latest) for your version of the game and place it in `SteamLibrary\steamapps\common\Schedule I\Mods`.
4. Run the game. The mod is enabled automatically and requires no further setup.
5. To update the mod, follow step 3 again. To remove the mod, delete it and its configuration file from your Mods folder.

## Building

1. The mod exists in two versions. Select the project folder based on the version of the game you are intending to run:
    - Default: IL2CPP (.NET 6.0)
    - Alternate: Mono (.NET Framework 3.5)
2. You will likely need to update the references in the `.csproj` to point to wherever your game is installed on your machine. Remember to update the post build path as well.
    - You can either do this manually or by using the Visual Studio 2022 Template provided in the [MelonLoader quickstart](https://melonwiki.xyz/#/modders/quickstart?id=visual-studio-template).
3. Run `dotnet build` in the project folder. The `.dll` will be copied over to the `\Mods` folder in the game files directly.

## License & Credits

[MelonLoader](https://github.com/LavaGang/MelonLoader) is licensed under the Apache License, Version 2.0. See [LICENSE](https://github.com/LavaGang/MelonLoader/blob/master/LICENSE.md) for the full License.

This mod is not sponsored by, affiliated with or endorsed by Unity Technologies or its affiliates.
"Unity" is a trademark or a registered trademark of Unity Technologies or its affiliates in the U.S. and elsewhere.
