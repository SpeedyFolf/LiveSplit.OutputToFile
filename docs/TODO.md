# TODO

Open work in this repo, by three-letter code. Each code links to its section in the detail doc named in the group heading: what's wrong or wanted, the evidence, a plan, how to verify it, and which docs to update. Within a group, rows are in suggested order of work. Each detail doc notes the dependencies between its plans.

When an item is finished, delete its row here and its section in the detail doc, and log it in [DONE.md](DONE.md). When work turns up a new defect that isn't being fixed now, add both. An item decided not to be worth fixing moves to the last group, Won't fix.

## Defects — [Defects.md](Defects.md)

| Code | Area | Summary | Severity |
|---|---|---|---|

## Performance — [Performance.md](Performance.md)

| Code | Area | Summary | Severity |
|---|---|---|---|

## Cleanup — [Cleanup.md](Cleanup.md)

| Code | Area | Summary | Severity |
|---|---|---|---|

## Features and limitations — [Features.md](Features.md)

| Code | Area | Summary | Severity |
|---|---|---|---|
| [RMN](Features.md#rmn--no-list-of-the-remaining-splits) | Split list | Add `RemainingSplits\`, the counterpart of `CompletedSplits\`: every member not yet completed, the current one included (all before the start, none after the finish), without the completed lines' files, with a setting, off by default | Medium |
| [ALL](Features.md#all--no-list-of-every-split) | Split list | Add a list of every member of each level in every phase, with all of `SplitList\`'s files, with a setting, off by default. Name its folder first: `AllSplits` clashes with the level tag | Medium |

## Project and README — [Project.md](Project.md)

| Code | Area | Summary | Severity |
|---|---|---|---|

## Won't fix — [WontFix.md](WontFix.md)

Known and measured, but not worth fixing. Kept with their evidence and plan, in case that changes.

| Code | Area | Summary | Severity |
|---|---|---|---|
| [ATM](WontFix.md#atm--file-writes-arent-atomic) | File writing | Writes aren't atomic. Measured: an OBS text source on a timer file goes blank for 1–2 s about once every 4 h of running timer; on a custom variable that a script changes every frame, about once every 6 min (computed). Revisit only if blanks are seen | Low |
