using LiveSplit.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LiveSplit.UI.Components.OutputToFile {
	/// <summary>
	/// Writes SplitList\, CompletedSplits\, RemainingSplits\ and FullSplitList\. Every list writes the same files, for different ranges
	/// of splits, except that a list leaves out the files that only lines it never has would fill: completed lines, upcoming lines, or scrolling.
	/// FullSplitList\ is CompletedSplits\ and RemainingSplits\ joined, so their lines are built once for all three.
	/// </summary>
	class SplitListFiles : OutputWriter {

		const string HIGHLIGHT_FILL = "███████████████████████████████████████████";

		// Every split list writes the same files, each under its own folder, except those its lines never fill.
		// The FILE_LIST_* templates are relative to that folder: prefix one of the FOLDER_* constants.
		const string FOLDER_SPLITLIST = @"SplitList\";
		const string FOLDER_COMPLETEDSPLITS = @"CompletedSplits\";
		const string FOLDER_REMAININGSPLITS = @"RemainingSplits\";
		const string FOLDER_FULLSPLITLIST = @"FullSplitList\";

		const string FILE_LIST_NAMES = @"{1}\Names.txt";
		const string FILE_LIST_NAMES_RAW = @"{1}\Names_Raw.txt";
		const string FILE_LIST_NUMBERS = @"{1}\Numbers.txt";
		const string FILE_LIST_NUMBERS_IN_LEVEL = @"{1}\Numbers_{2}.txt";
		const string FILE_LIST_WHITESPACE = @"{1}\Names_Indent_{2}.txt";
		const string FILE_LIST_FINISH_TIME_LIVE = @"{1}\{0}\Time_Run_Current.txt";
		const string FILE_LIST_GOLD_DELTA = @"{1}\{0}\Delta_Gold.txt";
		const string FILE_LIST_GOLD_TIME_UPCOMING = @"{1}\{0}\Time_Gold_Upcoming.txt";
		const string FILE_LIST_CURRENT_SPLIT_HIGHLIGHT = @"{1}\Highlight_CurrentSplit.txt";
		const string FILE_LIST_SCROLLED_TO_SPLIT_HIGHLIGHT = @"{1}\Highlight_ScrolledToSplit.txt";
		const string FILE_LIST_GOLD_HIGHLIGHT = @"{1}\{0}\Highlight_Gold.txt";
		// {3} is the comparison's folder: these files are written once for each of Levels.Comparisons
		const string FILE_LIST_FINISH_TIME_UPCOMING = @"{1}\{0}\{3}\Time_Run_Upcoming.txt";
		const string FILE_LIST_SEGMENT_TIME_UPCOMING = @"{1}\{0}\{3}\Time_Segment_Upcoming.txt";
		const string FILE_LIST_RUN_DELTA = @"{1}\{0}\{3}\Delta_Run.txt";
		const string FILE_LIST_SEGMENT_DELTA = @"{1}\{0}\{3}\Delta_Segment.txt";
		const string FILE_LIST_AHEAD_GAINED_HIGHLIGHT = @"{1}\{0}\{3}\Highlight_AheadGained.txt";
		const string FILE_LIST_AHEAD_LOST_HIGHLIGHT = @"{1}\{0}\{3}\Highlight_AheadLost.txt";
		const string FILE_LIST_BEHIND_GAINED_HIGHLIGHT = @"{1}\{0}\{3}\Highlight_BehindGained.txt";
		const string FILE_LIST_BEHIND_LOST_HIGHLIGHT = @"{1}\{0}\{3}\Highlight_BehindLost.txt";

		// How far LiveSplit's scrolling has moved the split that every level's SplitList window is laid out around, in splits of the run,
		// down positive. Scroll() keeps that split within the run, so scrolling back moves it at once.
		int _scrollOffset;

		public SplitListFiles(LiveSplitState state, OutputToFileSettings settings, SplitLevels levels, FileQueue files) : base(state, settings, levels, files) { }

		/// <summary>
		/// Follows LiveSplit's scrolling: each notch moves the split the windows are laid out around by one split of the run.
		/// Call once per rewrite, after the levels are calculated.
		/// </summary>
		/// <param name="notches">The notches scrolled since the last call, down positive</param>
		/// <param name="backToCurrent">Whether to go back to the current split first</param>
		public void Scroll(int notches, bool backToCurrent) {
			if (backToCurrent) _scrollOffset = 0;
			// Notches past either end of the run are dropped, as LiveSplit's Splits component drops its own
			int split = Math.Min(Math.Max(UnscrolledSplit() + _scrollOffset + notches, 0), State.Run.Count - 1);
			_scrollOffset = split - UnscrolledSplit();
		}

		// The split the windows are laid out around, by index in the run
		int ScrolledToSplit() => UnscrolledSplit() + _scrollOffset;

		// The local index of the level's member that holds the scrolled-to split, or -1 while not scrolled
		int ScrolledToMember(int lvl) => _scrollOffset == 0 ? -1 : MemberAtOrAfter(lvl, ScrolledToSplit());

		// The split the windows are laid out around without scrolling, by index in the run: the current split,
		// the first split before the start, and the last after the finish
		int UnscrolledSplit() {
			if (State.CurrentPhase == TimerPhase.NotRunning) return 0;
			if (State.CurrentPhase == TimerPhase.Ended) return State.Run.Count - 1;
			return State.CurrentSplitIndex;
		}

		public void WriteSplitLists(TimingMethod method) {
			int startingLevel = 0;
			if (!Settings.OutputSubsplits) startingLevel = Levels.Count - 1;

			for (int lvl = startingLevel; lvl < Levels.Count; lvl++) {
				if (Settings.OutputSplitList) {
					var (first, last) = SplitListRange(lvl);
					WriteList(FOLDER_SPLITLIST, method, lvl, BuildList(method, lvl, first, last, completed: true, upcoming: true, scrolls: true));
				}

				// CompletedSplits\ and RemainingSplits\ divide the level at the previous split, and FullSplitList\ is the two joined:
				// each half is built once, if any of the three needs it
				int previous = GetPreviousSubsplitIndex(lvl);
				bool full = Settings.OutputFullSplitList;
				// Every completed split: none before the start, all after the finish. No upcoming lines, so no upcoming files.
				ListLines done = Settings.OutputCompletedSplits || full
					? BuildList(method, lvl, 0, previous, completed: true, upcoming: false, scrolls: false) : null;
				// The rest: all before the start, the current split and those after it, none after the finish. No completed lines, so no completed files.
				ListLines rest = Settings.OutputRemainingSplits || full
					? BuildList(method, lvl, previous + 1, Levels[lvl].Indices.Count - 1, completed: false, upcoming: true, scrolls: false) : null;
				if (Settings.OutputCompletedSplits) WriteList(FOLDER_COMPLETEDSPLITS, method, lvl, done);
				if (Settings.OutputRemainingSplits) WriteList(FOLDER_REMAININGSPLITS, method, lvl, rest);
				// Every split, in every phase. It has them all, so it doesn't scroll.
				if (full) WriteJoinedList(FOLDER_FULLSPLITLIST, method, lvl, done, rest);
			}
		}

		// The SplitList window at one level, as local indices: the member holding the scrolled-to split, with SplitsBefore members
		// before it and SplitsAfter after it, shifted to stay within the level. Without scrolling, that member is the current one,
		// the first before the start and the last after the finish.
		(int First, int Last) SplitListRange(int lvl) {
			int count = Levels[lvl].Indices.Count;
			int range = 1 + Settings.SplitsBefore + Settings.SplitsAfter;
			if (range > count) {
				return (0, count - 1);
			}
			int member = MemberAtOrAfter(lvl, ScrolledToSplit());
			int first = Math.Min(Math.Max(member - Settings.SplitsBefore, 0), count - range);
			return (first, first + range - 1);
		}

		// Builds the files of one split list at one level, for the splits with local indices first to last.
		// Without completed, the files of the completed splits are left out, so first must be after the previous split.
		// Without upcoming, the files of the current and upcoming splits are left out, so last must be at most the previous split.
		// Without scrolls, Highlight_ScrolledToSplit is left out.
		ListLines BuildList(TimingMethod method, int lvl, int first, int last, bool completed, bool upcoming, bool scrolls) {
			// -1 for none: no current split before the start or after the finish, nothing completed before the start
			int currentSubsplitIndex = GetCurrentSubsplitIndex(lvl);
			int previousSubsplitIndex = GetPreviousSubsplitIndex(lvl);
			int scrolledToIndex = ScrolledToMember(lvl);
			string[] finerLevels = Enumerable.Range(lvl + 1, Levels.Count - lvl - 1).Select(LevelTag).ToArray();
			var lines = new ListLines(Levels.Comparisons, completed, upcoming, scrolls, lvl + 1, finerLevels);

			for (int spl = first; spl <= last; spl++) {
				AppendNames(lines, lvl, spl);
				if (spl == currentSubsplitIndex) lines.CurrentSplitHighlight.Append(HIGHLIGHT_FILL);
				// Only a list that scrolls has it, though any list's split can be scrolled to
				if (spl == scrolledToIndex) lines.ScrolledToSplitHighlight?.Append(HIGHLIGHT_FILL);
				if (spl <= previousSubsplitIndex) {
					AppendCompletedLine(lines, method, lvl, spl);
				}
				else {
					AppendUpcomingLine(lines, method, lvl, spl);
				}
				lines.EndLine();
			}
			return lines;
		}

		// Writes a list's files into its folder
		void WriteList(string folder, TimingMethod method, int lvl, ListLines lines) {
			foreach (var file in lines.Files) {
				MakeFile(folder + file.Template, file.Lines.ToString(), method, lvl, file.ComparisonFolder);
			}
		}

		// Writes two lists as one: the lines of done, then those of rest. A file that only one of them has gets a blank line
		// for each of the other's splits, as one list over both ranges would have.
		void WriteJoinedList(string folder, TimingMethod method, int lvl, ListLines done, ListLines rest) {
			foreach (var (template, comparisonFolder) in done.Files.Concat(rest.Files).Select(f => (f.Template, f.ComparisonFolder)).Distinct()) {
				MakeFile(folder + template, done.Text(template, comparisonFolder) + rest.Text(template, comparisonFolder), method, lvl, comparisonFolder);
			}
		}

		// Names_Indent_{depth}.txt, with {1} left in for MakeFile
		static string IndentFile(int depth) => string.Format(FILE_LIST_WHITESPACE, "", "{1}", depth);

		// Numbers_{level}.txt, with {1} left in for MakeFile
		static string NumbersFile(string levelTag) => string.Format(FILE_LIST_NUMBERS_IN_LEVEL, "", "{1}", levelTag);

		void AppendNames(ListLines lines, int lvl, int spl) {
			int allSplitsIndex = Levels[lvl].Indices[spl];
			string name = Levels.Names[allSplitsIndex][lvl];
			lines.NamesRaw.Append(State.Run[allSplitsIndex].Name);
			lines.Names.Append(name);
			// The member's number in its level, as in CurrentSplit\Number.txt
			lines.Numbers.Append(spl + 1);
			// And in each finer level, which has every member of this one: a section's number there is that of the split ending it
			for (int i = 0; i < lines.FinerNumbers.Length; i++) {
				lines.FinerNumbers[i].Append(MemberAtOrAfter(lvl + 1 + i, allSplitsIndex) + 1);
			}
			// Write different levels of subsplits to different files, allowing the user to indent them in their OBS etc.
			lines.Indents[Levels.Depths[allSplitsIndex]].Append(name);
		}

		// A completed split: this attempt's time and deltas, and its highlight
		void AppendCompletedLine(ListLines lines, TimingMethod method, int lvl, int spl) {
			var split = State.Run[Levels[lvl].Indices[spl]];
			lines.RunTimeCurrent.Append(DurationString(split.SplitTime[method]));
			lines.GoldDelta.Append(DeltaString(GoldDelta(method, lvl, spl)));
			// A gold is filled in Highlight_Gold, and in none of a comparison's highlights. A skipped split has no time, and no highlight.
			bool isGold = IsGold(method, lvl, spl);
			bool highlighted = !isGold && LiveRunTime(method, lvl, spl) != null;
			if (isGold) lines.GoldHighlight.Append(HIGHLIGHT_FILL);
			foreach (var comparison in lines.Comparisons) {
				TimeSpan? runDelta = RunDelta(method, comparison.Name, lvl, spl);
				TimeSpan? segmentDelta = SegmentDelta(method, comparison.Name, lvl, spl);
				comparison.RunDelta.Append(DeltaString(runDelta));
				comparison.SegmentDelta.Append(DeltaString(segmentDelta));
				if (highlighted) HighlightFile(comparison, method, lvl, spl, runDelta, segmentDelta)?.Append(HIGHLIGHT_FILL);
			}
		}

		// The current split and those after it: their best segments, and each comparison's times
		void AppendUpcomingLine(ListLines lines, TimingMethod method, int lvl, int spl) {
			lines.GoldTimeUpcoming.Append(DurationString(Levels[lvl].BestSegmentTimes[method][spl]));
			foreach (var comparison in lines.Comparisons) {
				comparison.RunTimeUpcoming.Append(DurationString(ComparisonTime(method, comparison.Name, lvl, spl)));
				comparison.SegmentTimeUpcoming.Append(DurationString(Levels[lvl].ComparisonSegmentTimes[comparison.Name][method][spl]));
			}
		}

		// The comparison's highlight file that a completed split that isn't a gold is filled in, or null for none. The first that applies wins.
		StringBuilder HighlightFile(ComparisonLines lines, TimingMethod method, int lvl, int spl, TimeSpan? runDelta, TimeSpan? segmentDelta) {
			if (runDelta < TimeSpan.Zero && segmentDelta < TimeSpan.Zero) return lines.AheadGainedHighlight;
			if (runDelta < TimeSpan.Zero) return lines.AheadLostHighlight;
			if (segmentDelta < TimeSpan.Zero) return lines.BehindGainedHighlight;
			if (ComparisonTime(method, lines.Name, lvl, spl) != null) return lines.BehindLostHighlight;
			return null;
		}

		/// <summary>
		/// The files of one split list, built a line at a time: every file gets a line per split, left blank where it has nothing for that split.
		/// One StringBuilder per file, appended to directly. StringBuilders, not +=: CompletedSplits rebuilds every completed line on every split,
		/// and RemainingSplits every other line. FullSplitList joins the two.
		/// </summary>
		class ListLines {

			public readonly StringBuilder Names, NamesRaw, Numbers;
			// Only for completed splits, which RemainingSplits never has. Null there.
			public readonly StringBuilder RunTimeCurrent, GoldDelta, GoldHighlight;
			// Only for the current split and those after it, which CompletedSplits never has. Null there.
			public readonly StringBuilder GoldTimeUpcoming, CurrentSplitHighlight;
			// Only for scrolling, which only moves SplitList. Null elsewhere.
			public readonly StringBuilder ScrolledToSplitHighlight;
			// Names_Indent_{depth}, one per depth up to the level's
			public readonly StringBuilder[] Indents;
			// Numbers_{level}, one per level finer than the list's, in order
			public readonly StringBuilder[] FinerNumbers;
			// The files of each comparison, in the order of SplitLevels.Comparisons
			public readonly ComparisonLines[] Comparisons;

			// Every file above with its FILE_LIST_* template, and the comparison's folder for a comparison's file
			readonly List<(string Template, string ComparisonFolder, StringBuilder Lines)> _files = new List<(string, string, StringBuilder)>();
			// The same files, by template and comparison folder
			readonly Dictionary<(string, string), StringBuilder> _byFile = new Dictionary<(string, string), StringBuilder>();

			// The number of lines: one per split
			public int Count { get; private set; }

			public ListLines(List<(string Name, string Folder)> comparisons, bool completed, bool upcoming, bool scrolls, int depths, string[] finerLevels) {
				Names = Add(FILE_LIST_NAMES);
				NamesRaw = Add(FILE_LIST_NAMES_RAW);
				Numbers = Add(FILE_LIST_NUMBERS);
				if (completed) {
					RunTimeCurrent = Add(FILE_LIST_FINISH_TIME_LIVE);
					GoldDelta = Add(FILE_LIST_GOLD_DELTA);
					GoldHighlight = Add(FILE_LIST_GOLD_HIGHLIGHT);
				}
				if (upcoming) {
					GoldTimeUpcoming = Add(FILE_LIST_GOLD_TIME_UPCOMING);
					CurrentSplitHighlight = Add(FILE_LIST_CURRENT_SPLIT_HIGHLIGHT);
				}
				if (scrolls) {
					ScrolledToSplitHighlight = Add(FILE_LIST_SCROLLED_TO_SPLIT_HIGHLIGHT);
				}
				Indents = new StringBuilder[depths];
				for (int depth = 0; depth < depths; depth++) {
					Indents[depth] = Add(IndentFile(depth));
				}
				FinerNumbers = finerLevels.Select(level => Add(NumbersFile(level))).ToArray();
				Comparisons = comparisons.Select(c => new ComparisonLines(c.Name, completed, upcoming, template => Add(template, c.Folder))).ToArray();
			}

			StringBuilder Add(string template, string comparisonFolder = null) {
				var lines = new StringBuilder();
				_files.Add((template, comparisonFolder, lines));
				_byFile.Add((template, comparisonFolder), lines);
				return lines;
			}

			public IEnumerable<(string Template, string ComparisonFolder, StringBuilder Lines)> Files => _files;

			// A file's lines, or a blank line per split if the list doesn't have that file
			public string Text(string template, string comparisonFolder) {
				return _byFile.TryGetValue((template, comparisonFolder), out var lines) ? lines.ToString() : new string('\n', Count);
			}

			public void EndLine() {
				// '\n', not AppendLine, which would write "\r\n"
				foreach (var file in _files) {
					file.Lines.Append('\n');
				}
				Count++;
			}
		}

		/// <summary>
		/// The files of one list that compare with one comparison, in that comparison's folder
		/// </summary>
		class ComparisonLines {

			public readonly string Name;
			// Only for completed splits, which RemainingSplits never has. Null there.
			public readonly StringBuilder RunDelta, SegmentDelta;
			public readonly StringBuilder AheadGainedHighlight, AheadLostHighlight, BehindGainedHighlight, BehindLostHighlight;
			// Only for the current split and those after it, which CompletedSplits never has. Null there.
			public readonly StringBuilder RunTimeUpcoming, SegmentTimeUpcoming;

			// add makes the StringBuilder of a file, and adds it to the list's files
			public ComparisonLines(string name, bool completed, bool upcoming, Func<string, StringBuilder> add) {
				Name = name;
				if (completed) {
					RunDelta = add(FILE_LIST_RUN_DELTA);
					SegmentDelta = add(FILE_LIST_SEGMENT_DELTA);
					AheadGainedHighlight = add(FILE_LIST_AHEAD_GAINED_HIGHLIGHT);
					AheadLostHighlight = add(FILE_LIST_AHEAD_LOST_HIGHLIGHT);
					BehindGainedHighlight = add(FILE_LIST_BEHIND_GAINED_HIGHLIGHT);
					BehindLostHighlight = add(FILE_LIST_BEHIND_LOST_HIGHLIGHT);
				}
				if (upcoming) {
					RunTimeUpcoming = add(FILE_LIST_FINISH_TIME_UPCOMING);
					SegmentTimeUpcoming = add(FILE_LIST_SEGMENT_TIME_UPCOMING);
				}
			}
		}
	}
}
