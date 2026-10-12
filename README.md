# Worker Fixes

A [MelonLoader](https://melonwiki.xyz/) mod for Schedule I. Between tasks, employees stand beside their work instead of walking outside and waiting there.

It covers botanists, chemists, handlers, and cleaners. At the end of a shift, or when an employee is unpaid and cannot work, they still use the game's outdoor idle spot. With the 4:00 AM option on, every worker also walks back outside at 4:00 AM and stays there until morning.

## Install

1. Install MelonLoader for Schedule I. If `version.dll` does not load the loader, rename that proxy to `winhttp.dll` in the game folder.
2. Copy `WorkerFixes.dll` into the game's `Mods` folder.
3. Start the game.

The mod only changes idle spots while you are hosting the session.

## Where they stand

Each employee stands about a short step off an access point, on the navmesh. If that spot cannot be found, they keep the outdoor idle point.

- **Botanists** stand at the assigned pot, drying rack, or mushroom bed that will finish soonest. Ready work wins. If nothing is running, they stand at the closest assigned station.
- **Chemists** do the same for ovens, cauldrons, mixing stations, and chemistry stations.
- **Handlers** with packaging stations or brick presses stand at the closest one.
- **Handlers** with no stations stand beside the **From** of a route. That is the first route until you pick another.
- **Cleaners** stand at the closest assigned bin.

## Picking a handler's route

Open a single handler's management page and look at the route list. Each route has an **Idle** button. The route they will stand beside is green.

- Click a route's **Idle** button to stand beside that route's From.
- Click the green button again to go back to the first route.
- If the chosen route is deleted, they go back to the first route.
- The choice is saved for that handler and kept when you reload.
- The button is hidden while several handlers are selected at once.

Handlers who have packaging stations or brick presses still stand at the closest station. The route pick is used when they have neither.

## Settings

After the first launch, edit `UserData\MelonPreferences.cfg` under the `WorkerFixes` category. Restart the game after changing it.

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | true | Turn idle retargeting on or off. |
| StandDistance | 0 | How far, in meters, they stand off the access point. Values are kept between 0 and 4. |
| ReturnOutsideAt4Am | true | At 4:00 AM, send every worker back to the outdoor idle spot until morning. |
| DebugLogging | true | Write each idle decision to the MelonLoader log. The same decision is not repeated. |

Logs are in the game's `MelonLoader\Logs` folder. Lines from this mod start with `[Worker_Fixes]`.
