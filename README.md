<h1 align="center">Shelf Limiter</h1>

<p align="center">
  <img src="About/Preview.png" alt="Shelf Limiter" width="324">
</p>

**Shelf Limiter is a RimWorld 1.6 mod that lets players set separate per-resource maximums for vanilla shelves, nutrient paste hoppers, and stockpile zones.**

## Features

* Set a separate maximum for every resource.
* Leave a field empty to preserve normal vanilla behavior.
* Supports vanilla shelves, small shelves, nutrient paste hoppers, stockpile zones, and dumping stockpile zones.
* Linked shelves share one group-wide limit.
* Stockpile and dumping-zone limits apply across the entire zone, not per cell.
* Values automatically cap at each resource’s physical storage capacity.
* Lowering a limit makes only the excess amount eligible for hauling.
* Accounts for resources that pawns are already carrying to storage.
* Displays `Default` when no limit is configured and `Mixed` when selected storage targets have different limits.
* Pressing Enter commits the field and releases keyboard focus.
* Shows the currently stored amount and configured limit in the tooltip.
* Provides subtle status markers when storage is below, at, or above its limit.
* The **Reset limits** button clears every configured maximum without changing vanilla storage permissions.
* Vanilla copy/paste storage settings also copy the limits.
* Limits persist in saved games.

Other containers and modded storage buildings are not changed.

## Requirements

* RimWorld 1.6
* [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

## Installation

1. Download the latest release.
2. Extract the `Shelf Limiter` folder into RimWorld’s `Mods` directory.
3. Enable Harmony and Shelf Limiter in the mod list.
4. Keep Harmony above Shelf Limiter in the load order.

## Usage

Select supported storage and open its **Storage** tab. Enter a number beside any resource to set its maximum. Clear the field to restore default behavior.

Linked shelves share one group-wide maximum while vanilla decides how resources are distributed between them.

For stockpile and dumping stockpile zones, each limit applies across the entire zone rather than to each individual cell.

When multiple supported storage targets are selected, shared limits appear normally while differing limits display `Mixed`. Entering a number applies it to every selected target without linking them together.

If storage already contains more than its new maximum, pawns can haul the excess to another valid destination. Another storage destination must accept the resource and have free space.

## Compatibility

Shelf Limiter uses Harmony patches and does not replace vanilla definitions. Mods that heavily rewrite the storage interface or hauling logic may require compatibility work.

## License

The original Shelf Limiter source code is available under the [MIT License](LICENSE). RimWorld and any visual elements derived from it belong to Ludeon Studios. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
