# Deal-Optimizer-Mod
A MelonLoader mod that optimizes deals for the Steam game [Schedule I](https://store.steampowered.com/app/3164500/Schedule_I/)

> [!NOTE]
> All features of this mod are for both versions of the game (`Mono` and `IL2CPP`) unless otherwise stated. (See [Installation](https://github.com/xyrilyn/Deal-Optimizer-Mod/edit/main/README.md#installation) for more information.)

**Features:**
* Evaluates the current offer and displays its probability of success in the Counteroffer UI
* Displays the price per unit of the current offer in the Counteroffer UI
* Displays the maximum daily cash for the customer in the Counteroffer UI
* Attempts to set a price for the current quantity that has the highest probability of success during Counteroffers
* Automatically sets the maximum daily cash the customer has during Offers
* Adds a Product Evaluator window to the Product Manager App in your phone to check whether customers will buy your product (from yourself or their dealers) and for how much
* `[IL2CPP-only]` Supports mod configuration via the [Mod Manager Phone App mod](https://www.nexusmods.com/schedule1/mods/397) with live updates

## Installation
> [!NOTE]
> Schedule I has two versions: `Mono` and `IL2CPP`. You should pick the mod version based on the game version you are playing.
> 
> You can select your game version via Steam > Schedule I > Properties > Betas > Beta Participation. By default, it will be `none` (which is `IL2CPP`, the normal version of the game which most players will play on).
> 
> If you are playing using `none` or `beta`, use the `IL2CPP` version of this mod.
> 
> If you are playing using `alternate` or `alternate-beta`, use the `Mono` version of this mod.

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader) for the game.
2. After installing MelonLoader for the game, run the game once for MelonLoader to setup. Exit the game.
3. Download the [latest release](https://github.com/xyrilyn/Deal-Optimizer-Mod/releases/latest) for your version of the game and place it in `SteamLibrary\steamapps\common\Schedule I\Mods`.
4. Run the game. The mod is enabled automatically and requires no further setup.
5. To update the mod, follow step 3 again. To remove the mod, delete it and its configuration file from your Mods folder.

## Configuration
> [!TIP]
> `[Mono-only]`: `SteamLibrary\steamapps\common\Schedule I\Mods\DealOptimizer\DealOptimizer_Config.json`
> 
> `[IL2CPP-only]`: `SteamLibrary\steamapps\common\Schedule I\UserData\MelonPreferences.cfg`

* The configuration options below work for both versions of this mod - however, they are configured in different ways
* For Mono:
    * If the configuration file does not exist, it will be created on starting the game.
    * Exit the game. Manually edit `DealOptimizer_Config.json` in any text editor. Save the file and then run the game.
* For IL2CPP:
    * Method One: Exit the game. Manually edit `MelonPreferences.cfg` in any text editor. Save the file and then run the game.
    * Method Two: Install [Mod Manager Phone App mod](https://www.nexusmods.com/schedule1/mods/397) and use it to configure this mod.

| Options | Description | Default |
| - | - | - |
| CounterofferUIEnabled | Enable the Counteroffer UI | true |
| PricePerUnitDisplay | Display the price per unit in the Counteroffer UI | true |
| MaximumDailySpendDisplay | Display the customer's maximum daily spend in the Counteroffer UI | true |
| CounterofferOptimizationEnabled | Enable optimization for Counteroffers | true |
| MinimumSuccessProbability | Minimum success % for optimization | 98 |
| StreetDealOptimizationEnabled | Enable optimization for Street Deals | true |
| ProductEvaluatorEnabled | Enable Product Evaluator feature | false |
| PrintCalculationsToConsole | Print calculation steps to the MelonLoader console | false |

## Building

1. The mod exists in two versions. Select the project folder based on the version of the game you are intending to run:
    - Default: `IL2CPP` (.NET 6.0)
    - Alternate: `Mono` (.NET Framework 3.5)
2. You will likely need to update the references in the `.csproj` to point to wherever your game is installed on your machine. Remember to update the post build path as well.
    - You can either do this manually or by using the Visual Studio 2022 Template provided in the [MelonLoader quickstart](https://melonwiki.xyz/#/modders/quickstart?id=visual-studio-template).
3. Run `dotnet build` in the project folder. The `.dll` will be copied over to the `\Mods` folder in the game files directly.

## License & Credits

[MelonLoader](https://github.com/LavaGang/MelonLoader) is licensed under the Apache License, Version 2.0. See [LICENSE](https://github.com/LavaGang/MelonLoader/blob/master/LICENSE.md) for the full License.

This mod is not sponsored by, affiliated with or endorsed by Unity Technologies or its affiliates.
"Unity" is a trademark or a registered trademark of Unity Technologies or its affiliates in the U.S. and elsewhere.
