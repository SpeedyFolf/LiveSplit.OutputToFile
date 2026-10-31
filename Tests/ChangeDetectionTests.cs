using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace LiveSplit.UI.Components.OutputToFile.Tests {
	/// <summary>
	/// What makes Update() rewrite the files, and the files it empties when they're no longer written
	/// </summary>
	public class ChangeDetectionTests {

		const string NAMES = @"SplitList\AllSplits\Names.txt";

		[Fact]
		public void EditingTheSplitsRewritesTheFiles() {
			using (var run = TestRun.Base()) {
				run.Edit(r => r[0].Name = "-X1");
				Assert.Equal("X1", run.Lines(NAMES)[0]);
				run.Edit(r => r[0].PersonalBestSplitTime = TestRun.T(11));
				Assert.Equal("0:11", run.Lines(@"SplitList\AllSplits\GameTime\CurrentComparison\Time_Run_Upcoming.txt")[0]);
			}
		}

		[Fact]
		public void RenamingASplitWithoutAnEventRewritesTheFiles() {
			using (var run = TestRun.Base()) {
				run.Start();
				// LiveSplit's server command setcurrentsplitname renames the split in place, and raises no event
				run.State.Run[0].Name = "-Renamed";
				run.Tick();
				Assert.Equal("Renamed", run.Read(@"CurrentSplit\AllSplits\Name.txt"));
				Assert.Equal("Renamed", run.Lines(NAMES)[0]);
				// Without its dash, A2 becomes a member of SansSubsplits
				run.State.Run[1].Name = "A2";
				run.Tick();
				Assert.Equal(new[] { "A2", "Area A", "Area B" }, run.Lines(@"SplitList\SansSubsplits\Names.txt"));
			}
		}

		[Fact]
		public void ReplacingTheRunRewritesTheFiles() {
			using (var run = TestRun.Base()) {
				// Close Splits replaces the run without an event
				run.State.Run = TestRun.MakeRun(("One", 10, 9), ("Two", 20, 9));
				run.Tick();
				Assert.Equal(new[] { "One", "Two" }, run.Lines(NAMES));
			}
		}

		// The levels are rebuilt only when what they're calculated from changes. Each test below changes one of those mid-run,
		// with no phase change, and reads a file calculated from the levels.

		const string SEGMENTS = @"FullSplitList\AllSplits\GameTime\CurrentComparison\Time_Segment_Upcoming.txt";

		[Fact]
		public void EditingTheSplitsMidRunRebuildsTheLevels() {
			using (var run = TestRun.Base()) {
				run.Start();
				run.Split(9.5);
				run.Edit(r => r[3].PersonalBestSplitTime = TestRun.T(45));
				Assert.Equal(new[] { "", "0:10", "0:10", "0:15", "0:05" }, run.Lines(SEGMENTS));
				run.Edit(r => r[2].BestSegmentTime = TestRun.T(7));
				Assert.Equal(new[] { "", "0:09", "0:07", "0:09", "0:09" }, run.Lines(@"FullSplitList\AllSplits\GameTime\Time_Gold_Upcoming.txt"));
			}
		}

		[Fact]
		public void TurningSubsplitsOffMidRunRebuildsTheLevels() {
			using (var run = TestRun.Base()) {
				run.Start();
				run.Split(9.5);
				run.Settings.OutputSubsplits = false;
				run.Tick();
				Assert.Equal("", run.Read(@"CurrentSplit\SansSubsplits\Name.txt"));
				Assert.Equal("", run.Read("TotalSplits_SansSubsplits.txt"));
				Assert.Equal("A2", run.Read(@"CurrentSplit\AllSplits\Name.txt"));
				run.Settings.OutputSubsplits = true;
				run.Tick();
				Assert.Equal("Area A", run.Read(@"CurrentSplit\SansSubsplits\Name.txt"));
			}
		}

		[Fact]
		public void AllCustomComparisonsMidRunRebuildsTheLevels() {
			using (var run = TestRun.Base(r => r.AddComparison("Target", 9, 19, 29, 39, 49))) {
				run.Start();
				run.Split(9.5);
				run.Settings.AllCustomComparisons = true;
				run.Tick();
				Assert.Equal("+0.5", run.Read(@"PreviousSplit\AllSplits\GameTime\Target\RunDifference.txt"));
				Assert.Equal(new[] { "", "0:10", "0:10", "0:10", "0:10" }, run.Lines(@"FullSplitList\AllSplits\GameTime\Target\Time_Segment_Upcoming.txt"));
			}
		}

		[Fact]
		public void ComparisonChangedWithoutAnEventRebuildsTheLevels() {
			using (var run = TestRun.Base()) {
				run.Start();
				run.Split(9.5);
				// As LiveSplit's Settings dialog regenerating a comparison: every split time changes, the last one included, with no event
				for (int i = 0; i < run.State.Run.Count; i++) {
					run.State.Run[i].PersonalBestSplitTime = TestRun.T(11 * (i + 1));
				}
				run.Tick();
				Assert.Equal(new[] { "", "0:11", "0:11", "0:11", "0:11" }, run.Lines(SEGMENTS));
			}
		}

		[Fact]
		public void ResetAndStartInOneFrameRebuildsTheLevels() {
			using (var run = TestRun.Base()) {
				run.Start();
				run.Split(9);
				run.Split(18);
				run.Split(26.5);
				// Running before and after: only the reset count tells that the history gained this attempt, where Area A took 26.5
				run.Model.Reset();
				run.Start();
				Assert.Equal("0:26", run.Read(@"CurrentSplit\SansSubsplits\GameTime\GoldTime.txt"));
			}
		}

		[Fact]
		public void LiveSplitClearingTheMetadataRewritesTheFiles() {
			using (var run = TestRun.Base()) {
				run.Edit(r => {
					r.Metadata.RegionName = "USA / NTSC";
					r.Metadata.VariableValueNames["Variant"] = "Unrestricted";
				});
				// After downloading the game from speedrun.com, LiveSplit clears what the game doesn't have, with no event
				run.State.Run.Metadata.RegionName = "";
				run.Tick();
				Assert.Equal("", run.Read("RegionName.txt"));
				Assert.Equal("Unrestricted", run.Read("Variables.txt"));
				// It replaces the variables rather than changing them
				run.State.Run.Metadata.VariableValueNames = new Dictionary<string, string>();
				run.Tick();
				Assert.Equal("", run.Read("Variables.txt"));
				Assert.Equal("", run.Read(@"Variables\Variant.txt"));
			}
		}

		[Fact]
		public void TurningAnOutputOffEmptiesItsFiles() {
			using (var run = TestRun.Base()) {
				run.Start();
				run.Split(9.5);
				run.Settings.OutputTimer = false;
				run.Settings.OutputSplitList = false;
				run.Settings.OutputCompletedSplits = false;
				run.Settings.OutputRemainingSplits = false;
				run.Settings.OutputFullSplitList = false;
				run.Tick();
				Assert.Equal("", run.Read(@"Timers\GameTime\RunTimer.txt"));
				Assert.Equal("", run.Read(NAMES));
				Assert.Equal("", run.Read(@"CompletedSplits\AllSplits\Names.txt"));
				Assert.Equal("", run.Read(@"RemainingSplits\AllSplits\Names.txt"));
				Assert.Equal("", run.Read(@"FullSplitList\AllSplits\Names.txt"));
				Assert.Equal("A2", run.Read(@"CurrentSplit\AllSplits\Name.txt"));

				run.Settings.OutputSplitList = true;
				run.Tick();
				Assert.Equal("A1", run.Lines(NAMES)[0]);
			}
		}

		[Fact]
		public void TurningSubsplitsOffEmptiesTheOtherLevels() {
			using (var run = TestRun.Base()) {
				run.Settings.OutputSubsplits = false;
				run.Tick();
				Assert.Equal("", run.Read(@"CurrentSplit\SansSubsplits\Name.txt"));
				Assert.Equal("", run.Read("TotalSplits_SansSubsplits.txt"));
				Assert.Equal("5", run.Read("TotalSplits_AllSplits.txt"));
			}
		}

		[Fact]
		public void NewComponentEmptiesLeftoversAndKeepsOtherFiles() {
			using (var before = TestRun.Base()) {
				File.WriteAllText(Path.Combine(before.Folder, "Notes.txt"), "keep");
				File.WriteAllText(Path.Combine(before.Folder, @"CurrentSplit\Old.txt"), "old");
				using (var after = new TestRun(r => r.Settings.FolderPath = before.Folder, ("A", 10, 9), ("B", 20, 9))) {
					// SansSubsplits is a level the new run doesn't have
					Assert.Equal("", after.Read(@"CurrentSplit\SansSubsplits\Name.txt"));
					Assert.Equal("", after.Read("TotalSplits_SansSubsplits.txt"));
					Assert.Equal("", after.Read(@"CurrentSplit\Old.txt"));
					Assert.Equal("keep", after.Read("Notes.txt"));
					Assert.Equal("2", after.Read("TotalSplits_AllSplits.txt"));
				}
			}
		}

		[Fact]
		public void ResetWritesEveryFileAgain() {
			using (var run = TestRun.Base()) {
				string category = Path.Combine(run.Folder, "CategoryName.txt");
				run.Start();
				File.WriteAllText(category, "edited");
				// Unchanged contents aren't written again
				run.Split(9.5);
				Assert.Equal("edited", File.ReadAllText(category));
				run.Reset();
				Assert.Equal("Any%", File.ReadAllText(category));
			}
		}

		[Fact]
		public void ClosingLiveSplitWritesItsReset() {
			using (var closed = TestRun.Base())
			using (var reset = TestRun.Base()) {
				foreach (var run in new[] { closed, reset }) {
					run.Start();
					run.Split(9.5);
				}
				// LiveSplit resets the timer as it closes, then disposes the component with no frame in between
				closed.Model.Reset();
				closed.Component.Dispose();
				reset.Reset();
				Assert.Equal("-", closed.Read(@"CurrentSplit\AllSplits\Name.txt"));
				Assert.Equal(reset.AllFiles().Where(f => !f.Key.Contains("RealTime")), closed.AllFiles().Where(f => !f.Key.Contains("RealTime")));
			}
		}

		[Fact]
		public void DisposingWithNothingChangedWritesNothing() {
			using (var run = TestRun.Base()) {
				string category = Path.Combine(run.Folder, "CategoryName.txt");
				run.Start();
				File.WriteAllText(category, "edited");
				// As when another layout is opened
				run.Component.Dispose();
				Assert.Equal("edited", File.ReadAllText(category));
			}
		}
	}
}
