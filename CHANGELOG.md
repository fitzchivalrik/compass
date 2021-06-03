# CHANGELOG

## Unreleased

- feat: Add option to only show cardinals.
- feat: Add a mask percentage to visually cut off part of the compass.
  ![](docs/compass_mask_50.png)
- feat: Add chocobro companion to filter list
- fix: Update NaviMap/AreaMap reference on configuration change too.
  This allows one to easily reset the cached variables by enabling/disabling the compass.
  The cache e.g. needs to be reset after a DeepDungeon visit.
- fix: Add Gemstone trader to 'Shops' filter (closing #2)

## 1.0.0

- feat: Release

## 0.10.1-testing

- feat: Add filters
- fix: Various fixes under the hood

## 0.8.4-testing

- fix: Compatibility update to work with newest dalamud

## 0.8.1-testing

- feat: Position compass with X/Y values and center horizontally via a button
- feat: More hiding options
- feat: Line as background
- feat: Offset for cardinals
- feat: Scale in .01 units instead of .1
- fix: Miner gathering nodes should now be correctly handled 
- fix: Botanic gathering nodes should now be correctly handled
- fix: Glowing thingy under icons should now be correctly handled and spinning
- fix: Camera rotation is discarded in favour of reading the rotation from NaviMap
    This should fix the 'static' compass errors
- fix: 'Hide Compass in Combat' now properly checks even if no other hiding options are set

BREAKING:
- Default size for icons is now 8 pixel bigger.

## 0.5.9-testing

- feat: Hiding options are now its own tab
- fix: Crash bug on default install
- Various other small fixes

## 0.4.0-testing

- First testing release with basic functionality