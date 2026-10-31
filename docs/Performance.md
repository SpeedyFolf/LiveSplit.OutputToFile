# Performance

The index is [TODO.md](TODO.md).

## KLV — Keep the levels between splits

Every full rewrite calls `CalculateSegments()` in [OutputToFileComponent.cs](../UI/Components/OutputToFileComponent.cs). It rebuilds the shared `SplitLevels` from the run: the levels and their members, the comparisons to write, each comparison's segment times, and the best segments. A split, skip, undo, pause or resume changes none of that. It changes only on:
- a reset, which updates the best segments and the segment history;
- a run edit, or another run loaded or closed;
- the Subsplits setting;
- the selected comparison, the ticked comparisons, or All custom comparisons;
- a comparison renamed or removed;
- a split renamed, which can change its depth.

**Evidence.** Profiled 2026-10-30, headless, Release, on the 1,317-split, seven-level GTA SA file, over 200 splits from split 329. `CalculateSegments` took 3.7–4.5 ms of each split's `Update()`, whichever lists were on. That's about a third of a split with only `SplitList\` and `CompletedSplits\` on (11 ms). On a 40-split file a whole split takes 0.6 ms (Debug), so only very long splits files would notice.

**Plan.**
- Give `Update()` a second key set, for the levels' inputs only:
  - the run's reference, the run edits and the resets;
  - `OutputSubsplits`, `CurrentComparison`, `TickedComparisons` and `AllCustomComparisons`;
  - the comparison checks;
  - `SplitNamesChanged`.
- Rebuild the levels only when that set has changed. A rewrite caused by anything else keeps them.
- Keep calling `_timerFiles.SegmentStartsChanged()` on every rewrite: the timer phase and the current split change what the segment timers count from.
- The levels still hold nothing about the current attempt, but the rule that every rewrite rebuilds them goes. Update CLAUDE.md's "Nothing per attempt is stored" bullet and architecture.md's "Every rewrite rebuilds the levels" sentence.

**Verify.**
- All tests pass, and the snapshots are unchanged.
- Add a test for each input above that changes the levels mid-run, checking that the files follow it.
- Time a split on the GTA SA file before and after, as in architecture.md's "Cost of the long lists".

**Docs.** architecture.md (the update loop, Per-level data, the cost figures) and CLAUDE.md.
