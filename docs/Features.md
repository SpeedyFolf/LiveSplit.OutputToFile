# Features and limitations

Things the component doesn't do yet. The index is [TODO.md](TODO.md).

RMN and ALL both add a list to `SplitListFiles`, where `WriteSplitListLevel` writes every list. So each new list gets the files that every list has, such as `Names.txt` and `Numbers.txt`, with no work of its own. Each can be done alone, but in this order each needs less work:
- RMN first: it's the first list with no completed lines, so it makes `ListLines` able to leave out those lines' files.
- ALL then needs only one more list, with the files of both kinds of line.

## RMN — No list of the remaining splits

`CompletedSplits\` lists every member done so far, but nothing lists the rest. `SplitList\` shows only its window, and `CurrentSplit\Remaining.txt` only their count. Wanted: `RemainingSplits\{level}\`, a list of every member not yet completed, the current one included. It gets a Remaining splits setting, off by default. Its members are the ones `Remaining.txt` counts: all of them before the start, the current one and those after it during the run, and none after the finish.

**Evidence.** Read in code: `WriteSplitLists` writes `SplitList\` over `SplitListRange`, and `CompletedSplits\` over 0 to `GetPreviousSubsplitIndex(lvl)`. It writes no other list.

**Plan.**
- Write it from `WriteSplitLists`, over the local indices from `GetPreviousSubsplitIndex(lvl) + 1` to the level's last. In every phase, that's the members `CompletedSplits\` doesn't have. After the finish the range is empty, so the files are written empty, as `CompletedSplits\`'s are before the start.
- Leave out the files that only completed lines fill, since here they would always be blank: `Time_Run_Current.txt`, the `Delta_*.txt` files, `Highlight_Gold.txt` and each comparison's four highlights. `ListLines` and `ComparisonLines` then need to know whether a list has completed lines, as `upcoming` tells them whether it has upcoming ones.
- It doesn't scroll, so leave out `Highlight_ScrolledToSplit.txt` too. Today `upcoming` decides that file as well, so it needs a flag of its own. Replace the flags with what a list has: completed lines, upcoming lines, scrolling.
- Keep `Highlight_CurrentSplit.txt`. It fills the first line during the run, and no line before the start, when no member is current.
- Add the setting as CLAUDE.md's "Adding a setting" says: `OutputRemainingSplits`, a checkbox beside Completed splits, an XML key that is `false` in older layouts, and a `Cache` key. Turn it on in `TestRun`, with the other outputs.
- Add `RemainingSplits` to `OUTPUT_FOLDERS`, so its files are emptied when the setting is turned off.

**Verify.**
- Tests:
  - Before the start the list holds every member, with no current line.
  - Mid-run it starts at the current member. In `SansSubsplits` it starts at the current section, while on one of its subsplits.
  - After the finish its files are empty.
  - Turning the setting off empties them.
  - The setting survives a save and load, and an older layout loads it as off.
- The snapshot diff shows `RemainingSplits\` with the files above, and nothing else changed.

**Docs.**
- output-files.md: a `RemainingSplits` section, and the list of outputs that are emptied when turned off.
- README.md: the setting, the list files, the folders the component treats as its own, and the tree.
- architecture.md: the write order, `OUTPUT_FOLDERS`, and the settings tables.
- CLAUDE.md: "One list writer", and the `SplitListFiles` row.
- CHANGELOG.md: under Unreleased, Added.

## ALL — No list of every split

Wanted: a list of every member of each level, in every phase, with all of `SplitList\`'s files. Completed lines show this attempt's times, deltas and highlights, and the current and upcoming lines show the comparisons' times. It gets a setting, off by default. In effect it's `CompletedSplits\` and the remaining splits ([RMN](#rmn--no-list-of-the-remaining-splits)) in one list, or `SplitList\` with a window as long as the level.

**Evidence.** Read in code:
- `SplitListRange` already gives the whole level when `SplitsBefore + 1 + SplitsAfter` is at least the level's count. But then the windowed list is gone.
- The Designer sets no `Maximum` on either `NumericUpDown`, so WinForms' default of 100 caps the window at 201 members.

**Plan.**
- Pick the folder name first. `AllSplits` is the last level's tag, so `AllSplits\AllSplits\` and `AllSplits\SansSubsplits\` would be confusing. Suggested: `FullSplitList\`.
- Write it from `WriteSplitLists`, over every local index from 0 to the level's last, with completed and upcoming lines as in `SplitList\`.
- It doesn't scroll, since every member is already in it, so leave out `Highlight_ScrolledToSplit.txt`, as `CompletedSplits\` does.
- The setting, `TestRun` and `OUTPUT_FOLDERS` as for RMN.

**Verify.**
- Tests: before the start, mid-run and after the finish, every member has a line, and the lines up to the previous member are completed ones. `Highlight_CurrentSplit.txt` fills the current member's line. Turning the setting off empties the files, and the setting survives a save and load.
- The snapshot diff shows only the new folder. Mid-run, each of its files has the lines of `CompletedSplits\` followed by those of `RemainingSplits\`, if RMN is done.
- Cost: every rewrite rebuilds every line of every level, timing method and comparison, as `CompletedSplits\` does at the finish. On the 1,317-split GTA SA file, time a split's `Update()` with the setting off and on, and add the figure to architecture.md.

**Docs.** As for RMN, for the new folder and setting. architecture.md also gets the cost figure.
