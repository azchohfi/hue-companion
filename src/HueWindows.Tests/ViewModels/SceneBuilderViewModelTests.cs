using System.Collections.ObjectModel;
using FluentAssertions;
using HueWindows.Core.Models;
using HueWindows.Core.Services;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;
using Moq;
using Xunit;

namespace HueWindows.Tests.ViewModels;

public class SceneBuilderViewModelTests
{
    private readonly Mock<IMultiBridgeService> _multiBridgeMock = new();
    private readonly Mock<IAnimationService> _animationMock = new();
    private readonly Mock<ISceneStorageService> _storageMock = new();
    private readonly SceneBuilderViewModel _vm;

    public SceneBuilderViewModelTests()
    {
        _vm = new SceneBuilderViewModel(
            _multiBridgeMock.Object,
            _animationMock.Object,
            _storageMock.Object);
    }

    #region Snap to Grid

    [Fact]
    public void SnapToGrid_WhenEnabled_SnapsToNearestInterval()
    {
        _vm.IsSnapEnabled = true;
        _vm.ZoomLevel = 50; // SnapInterval = 1.0

        _vm.SnapToGrid(1.3).Should().Be(1.0);
        _vm.SnapToGrid(1.7).Should().Be(2.0);
        _vm.SnapToGrid(1.5).Should().Be(2.0); // rounds up at midpoint
    }

    [Fact]
    public void SnapToGrid_WhenDisabled_ReturnsExactValue()
    {
        _vm.IsSnapEnabled = false;

        _vm.SnapToGrid(1.37).Should().Be(1.37);
    }

    [Theory]
    [InlineData(30, 2.0)]   // Zoomed out
    [InlineData(50, 1.0)]   // Medium
    [InlineData(100, 0.5)]  // Zoomed in
    [InlineData(200, 0.25)] // Fully zoomed
    public void SnapInterval_ChangesWithZoomLevel(double zoom, double expectedInterval)
    {
        _vm.ZoomLevel = zoom;
        _vm.SnapInterval.Should().Be(expectedInterval);
    }

    #endregion

    #region Keyframe CRUD

    [Fact]
    public void AddKeyframe_InsertsInSortedOrder()
    {
        var track = CreateTrackWithKeyframes(0, 4, 8);
        _vm.Tracks.Add(track);

        _vm.AddKeyframe(track, 2.0);

        track.Keyframes.Should().HaveCount(4);
        track.Keyframes[0].TimeSeconds.Should().Be(0);
        track.Keyframes[1].TimeSeconds.Should().Be(2.0);
        track.Keyframes[2].TimeSeconds.Should().Be(4);
        track.Keyframes[3].TimeSeconds.Should().Be(8);
    }

    [Fact]
    public void AddKeyframe_InterpolatesColorFromSurroundingKeyframes()
    {
        var red = new HueColor(0.68, 0.31);
        var blue = new HueColor(0.15, 0.06);

        var track = new TrackViewModel
        {
            LightId = Guid.NewGuid().ToString(),
            DisplayName = "Test",
            Keyframes = new ObservableCollection<KeyframeViewModel>
            {
                new() { TimeSeconds = 0, Color = red, Brightness = 1.0 },
                new() { TimeSeconds = 4, Color = blue, Brightness = 0.2 },
            }
        };
        _vm.Tracks.Add(track);

        _vm.AddKeyframe(track, 2.0); // Midpoint

        var added = track.Keyframes[1];
        added.TimeSeconds.Should().Be(2.0);
        // Brightness should be interpolated to ~0.6 (midpoint of 1.0 and 0.2)
        added.Brightness.Should().BeApproximately(0.6, 0.01);
        // Color should be neither red nor blue but somewhere in between
        added.Color.Should().NotBe(red);
        added.Color.Should().NotBe(blue);
    }

    [Fact]
    public void AddKeyframe_SetsSelectedAndMarksDirty()
    {
        var track = CreateTrackWithKeyframes(0, 8);
        _vm.Tracks.Add(track);
        _vm.HasUnsavedChanges = false;

        _vm.AddKeyframe(track, 4.0);

        _vm.SelectedKeyframe.Should().NotBeNull();
        _vm.SelectedKeyframe!.TimeSeconds.Should().Be(4.0);
        _vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void DeleteKeyframe_RemovesMiddleKeyframe()
    {
        var track = CreateTrackWithKeyframes(0, 2, 4);
        _vm.Tracks.Add(track);
        var middle = track.Keyframes[1];

        _vm.DeleteKeyframeCommand.Execute(middle);

        track.Keyframes.Should().HaveCount(2);
        track.Keyframes.Select(k => k.TimeSeconds).Should().BeEquivalentTo(new[] { 0.0, 4.0 });
    }

    [Fact]
    public void DeleteKeyframe_ProtectsFirstKeyframe()
    {
        var track = CreateTrackWithKeyframes(0, 2, 4);
        _vm.Tracks.Add(track);
        var first = track.Keyframes[0];

        _vm.DeleteKeyframeCommand.Execute(first);

        track.Keyframes.Should().HaveCount(3); // Unchanged
    }

    [Fact]
    public void DeleteKeyframe_ProtectsLastKeyframe()
    {
        var track = CreateTrackWithKeyframes(0, 2, 4);
        _vm.Tracks.Add(track);
        var last = track.Keyframes[2];

        _vm.DeleteKeyframeCommand.Execute(last);

        track.Keyframes.Should().HaveCount(3); // Unchanged
    }

    [Fact]
    public void DuplicateKeyframe_CreatesOffsetCopy()
    {
        _vm.DurationSeconds = 16;
        var track = CreateTrackWithKeyframes(0, 4, 8);
        _vm.Tracks.Add(track);
        var original = track.Keyframes[1]; // t=4

        _vm.DuplicateKeyframeCommand.Execute(original);

        track.Keyframes.Should().HaveCount(4);
        // Duplicate at t=5 (original + 1.0)
        var duplicate = _vm.SelectedKeyframe;
        duplicate.Should().NotBeNull();
        duplicate!.TimeSeconds.Should().Be(5.0);
        duplicate.Color.X.Should().Be(original.Color.X);
        duplicate.Brightness.Should().Be(original.Brightness);
    }

    [Fact]
    public void DuplicateKeyframe_ClampsWhenAtEnd()
    {
        _vm.DurationSeconds = 8;
        var track = CreateTrackWithKeyframes(0, 4, 8);
        _vm.Tracks.Add(track);
        var last = track.Keyframes[2]; // t=8, at DurationSeconds

        _vm.DuplicateKeyframeCommand.Execute(last);

        // newTime = 8 + 1 = 9 > DurationSeconds, so falls back to 8 - 1 = 7
        var duplicate = _vm.SelectedKeyframe;
        duplicate!.TimeSeconds.Should().Be(7.0);
    }

    #endregion

    #region Duration Change

    [Fact]
    public void DurationSecondsChanged_ClampsKeyframesBeyondNewDuration()
    {
        var track = CreateTrackWithKeyframes(0, 5, 10);
        _vm.Tracks.Add(track);

        _vm.DurationSeconds = 6;

        // Keyframe at t=10 should be clamped to 6
        track.Keyframes[2].TimeSeconds.Should().Be(6);
        // Keyframe at t=5 should be unchanged (within bounds)
        track.Keyframes[1].TimeSeconds.Should().Be(5);
    }

    #endregion

    #region Dirty Indicator and PageTitle

    [Fact]
    public void PageTitle_ShowsAsteriskWhenDirty()
    {
        _vm.HasUnsavedChanges = false;
        _vm.PageTitle.Should().Be("Scene Builder");

        _vm.HasUnsavedChanges = true;
        _vm.PageTitle.Should().Be("Scene Builder *");
    }

    [Fact]
    public void SceneNameChanged_MarksDirty()
    {
        _vm.HasUnsavedChanges = false;

        _vm.SceneName = "New Name";

        _vm.HasUnsavedChanges.Should().BeTrue();
    }

    #endregion

    #region NewScene

    [Fact]
    public void NewScene_ResetsAllState()
    {
        // Set up some state
        _vm.SceneName = "My Scene";
        _vm.SceneDescription = "Description";
        _vm.DurationSeconds = 32;
        _vm.Tracks.Add(CreateTrackWithKeyframes(0, 4));
        _vm.EventTracks.Add(new EventTrackViewModel());
        _vm.HasUnsavedChanges = true;
        _vm.IsPlaying = true;
        _vm.PlayheadPosition = 5.0;

        _vm.NewScene();

        _vm.SceneName.Should().Be("Untitled Scene");
        _vm.SceneDescription.Should().Be("");
        _vm.DurationSeconds.Should().Be(16);
        _vm.Tracks.Should().BeEmpty();
        _vm.EventTracks.Should().BeEmpty();
        _vm.SelectedKeyframe.Should().BeNull();
        _vm.SelectedEventTrack.Should().BeNull();
        _vm.PlayheadPosition.Should().Be(0);
        _vm.IsPlaying.Should().BeFalse();
        _vm.HasUnsavedChanges.Should().BeFalse();
        _vm.IsEditingExistingScene.Should().BeFalse();
    }

    #endregion

    #region Multi-Select

    [Fact]
    public void ToggleKeyframeSelection_WithoutShift_ReplaceSelection()
    {
        var kf1 = new KeyframeViewModel { TimeSeconds = 1 };
        var kf2 = new KeyframeViewModel { TimeSeconds = 2 };

        _vm.ToggleKeyframeSelection(kf1, isShiftClick: false);
        _vm.SelectedKeyframes.Should().ContainSingle().Which.Should().Be(kf1);

        _vm.ToggleKeyframeSelection(kf2, isShiftClick: false);
        _vm.SelectedKeyframes.Should().ContainSingle().Which.Should().Be(kf2);
    }

    [Fact]
    public void ToggleKeyframeSelection_WithShift_AddsToSelection()
    {
        var kf1 = new KeyframeViewModel { TimeSeconds = 1 };
        var kf2 = new KeyframeViewModel { TimeSeconds = 2 };

        _vm.ToggleKeyframeSelection(kf1, isShiftClick: false);
        _vm.ToggleKeyframeSelection(kf2, isShiftClick: true);

        _vm.SelectedKeyframes.Should().HaveCount(2);
        _vm.SelectedKeyframes.Should().Contain(kf1);
        _vm.SelectedKeyframes.Should().Contain(kf2);
    }

    [Fact]
    public void ToggleKeyframeSelection_WithShift_TogglesExisting()
    {
        var kf1 = new KeyframeViewModel { TimeSeconds = 1 };

        _vm.ToggleKeyframeSelection(kf1, isShiftClick: false);
        _vm.ToggleKeyframeSelection(kf1, isShiftClick: true); // Deselect

        _vm.SelectedKeyframes.Should().BeEmpty();
    }

    [Fact]
    public void SelectKeyframesInRect_SelectsCorrectKeyframes()
    {
        var track0 = CreateTrackWithKeyframes(1, 3, 5, 7);
        var track1 = CreateTrackWithKeyframes(2, 4, 6, 8);
        _vm.Tracks.Add(track0);
        _vm.Tracks.Add(track1);

        // Select rectangle from t=2.5 to t=5.5, tracks 0-1
        _vm.SelectKeyframesInRect(2.5, 5.5, 0, 1);

        _vm.SelectedKeyframes.Should().HaveCount(3);
        _vm.SelectedKeyframes.Should().Contain(track0.Keyframes[1]); // t=3
        _vm.SelectedKeyframes.Should().Contain(track0.Keyframes[2]); // t=5
        _vm.SelectedKeyframes.Should().Contain(track1.Keyframes[1]); // t=4
    }

    [Fact]
    public void ClearSelection_ClearsAll()
    {
        var kf = new KeyframeViewModel { TimeSeconds = 1 };
        _vm.ToggleKeyframeSelection(kf, isShiftClick: false);

        _vm.ClearSelection();

        _vm.SelectedKeyframes.Should().BeEmpty();
        _vm.SelectedKeyframe.Should().BeNull();
    }

    #endregion

    #region Undo/Redo

    [Fact]
    public void ExecuteCommand_UndoRestoresState()
    {
        var track = CreateTrackWithKeyframes(0, 8);
        _vm.Tracks.Add(track);
        var newKf = new KeyframeViewModel { TimeSeconds = 4, Color = HueColors.WarmWhite, Brightness = 1.0 };

        _vm.ExecuteCommand(new AddKeyframeCommand(track, newKf));
        track.Keyframes.Should().HaveCount(3);

        _vm.Undo();
        track.Keyframes.Should().HaveCount(2);
    }

    [Fact]
    public void ExecuteCommand_RedoReapplies()
    {
        var track = CreateTrackWithKeyframes(0, 8);
        _vm.Tracks.Add(track);
        var newKf = new KeyframeViewModel { TimeSeconds = 4, Color = HueColors.WarmWhite, Brightness = 1.0 };

        _vm.ExecuteCommand(new AddKeyframeCommand(track, newKf));
        _vm.Undo();
        _vm.Redo();

        track.Keyframes.Should().HaveCount(3);
        track.Keyframes.Should().Contain(newKf);
    }

    [Fact]
    public void CanUndo_ReflectsHistoryState()
    {
        _vm.CanUndo.Should().BeFalse();

        var track = CreateTrackWithKeyframes(0, 8);
        _vm.Tracks.Add(track);
        _vm.ExecuteCommand(new AddKeyframeCommand(track,
            new KeyframeViewModel { TimeSeconds = 4 }));

        _vm.CanUndo.Should().BeTrue();

        _vm.Undo();
        _vm.CanUndo.Should().BeFalse();
    }

    #endregion

    #region Copy/Paste

    [Fact]
    public void CopyPaste_PastesAtPlayheadWithRelativeOffsets()
    {
        var track = CreateTrackWithKeyframes(0, 2, 4, 8);
        _vm.Tracks.Add(track);
        _vm.DurationSeconds = 16;

        // Select keyframes at t=2 and t=4
        _vm.ToggleKeyframeSelection(track.Keyframes[1], isShiftClick: false);
        _vm.ToggleKeyframeSelection(track.Keyframes[2], isShiftClick: true);

        _vm.CopyKeyframes();

        // Move playhead and paste
        _vm.PlayheadPosition = 10.0;
        _vm.PasteKeyframes();

        // Should have 6 keyframes now: 0, 2, 4, 8, 10, 12
        track.Keyframes.Should().HaveCount(6);
        track.Keyframes.Select(k => k.TimeSeconds).Should().Contain(10.0);
        track.Keyframes.Select(k => k.TimeSeconds).Should().Contain(12.0);
    }

    [Fact]
    public void Paste_ClampsKeyframesBeyondDuration()
    {
        var track = CreateTrackWithKeyframes(0, 4, 8);
        _vm.Tracks.Add(track);
        _vm.DurationSeconds = 10;

        _vm.ToggleKeyframeSelection(track.Keyframes[1], isShiftClick: false); // t=4
        _vm.ToggleKeyframeSelection(track.Keyframes[2], isShiftClick: true);  // t=8

        _vm.CopyKeyframes();

        // Paste at t=8 — second keyframe would be at 8 + 4 = 12, beyond duration
        _vm.PlayheadPosition = 8.0;
        _vm.PasteKeyframes();

        // Only the first should paste (t=8), the second (t=12) is out of bounds
        track.Keyframes.Count(k => k.TimeSeconds == 8.0).Should().Be(2); // original + paste
        track.Keyframes.Should().NotContain(k => k.TimeSeconds > _vm.DurationSeconds);
    }

    #endregion

    #region DeleteSelectedKeyframes

    [Fact]
    public void DeleteSelectedKeyframes_RemovesMiddleKeyframes()
    {
        var track = CreateTrackWithKeyframes(0, 2, 4, 6, 8);
        _vm.Tracks.Add(track);

        // Select middle ones
        _vm.ToggleKeyframeSelection(track.Keyframes[1], isShiftClick: false); // t=2
        _vm.ToggleKeyframeSelection(track.Keyframes[2], isShiftClick: true);  // t=4
        _vm.ToggleKeyframeSelection(track.Keyframes[3], isShiftClick: true);  // t=6

        _vm.DeleteSelectedKeyframes();

        track.Keyframes.Should().HaveCount(2);
        track.Keyframes.Select(k => k.TimeSeconds).Should().BeEquivalentTo(new[] { 0.0, 8.0 });
    }

    [Fact]
    public void DeleteSelectedKeyframes_ProtectsFirstAndLast()
    {
        var track = CreateTrackWithKeyframes(0, 4, 8);
        _vm.Tracks.Add(track);

        // Select all three
        _vm.ToggleKeyframeSelection(track.Keyframes[0], isShiftClick: false);
        _vm.ToggleKeyframeSelection(track.Keyframes[1], isShiftClick: true);
        _vm.ToggleKeyframeSelection(track.Keyframes[2], isShiftClick: true);

        _vm.DeleteSelectedKeyframes();

        // First (0) and last (8) should survive, middle (4) deleted
        track.Keyframes.Should().HaveCount(2);
        track.Keyframes.Select(k => k.TimeSeconds).Should().BeEquivalentTo(new[] { 0.0, 8.0 });
    }

    #endregion

    #region Event Tracks

    [Fact]
    public void AddEventTrack_AddsAndSelects()
    {
        _vm.AddEventTrackCommand.Execute(EventPreset.LightningFlash);

        _vm.EventTracks.Should().HaveCount(1);
        _vm.SelectedEventTrack.Should().Be(_vm.EventTracks[0]);
        _vm.SelectedEventTrack!.Preset.Should().Be(EventPreset.LightningFlash);
        _vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void DeleteEventTrack_RemovesAndClearsSelection()
    {
        _vm.AddEventTrackCommand.Execute(EventPreset.Sparkle);
        var eventTrack = _vm.EventTracks[0];

        _vm.DeleteEventTrackCommand.Execute(eventTrack);

        _vm.EventTracks.Should().BeEmpty();
        _vm.SelectedEventTrack.Should().BeNull();
    }

    [Fact]
    public void SelectKeyframe_ClearsEventTrackSelection()
    {
        _vm.AddEventTrackCommand.Execute(EventPreset.LightningFlash);
        _vm.SelectedEventTrack.Should().NotBeNull();

        var kf = new KeyframeViewModel { TimeSeconds = 1 };
        _vm.SelectKeyframeCommand.Execute(kf);

        _vm.SelectedKeyframe.Should().Be(kf);
        _vm.SelectedEventTrack.Should().BeNull();
    }

    [Fact]
    public void SelectEventTrack_ClearsKeyframeSelection()
    {
        var kf = new KeyframeViewModel { TimeSeconds = 1 };
        _vm.SelectKeyframeCommand.Execute(kf);
        _vm.SelectedKeyframe.Should().NotBeNull();

        var eventTrack = new EventTrackViewModel();
        _vm.SelectEventTrackCommand.Execute(eventTrack);

        _vm.SelectedEventTrack.Should().Be(eventTrack);
        _vm.SelectedKeyframe.Should().BeNull();
    }

    #endregion

    #region EventTrackViewModel

    [Theory]
    [InlineData(0.1, 8.0, 15.0)]  // Rare
    [InlineData(0.5, 3.0, 6.0)]   // Medium
    [InlineData(0.9, 0.5, 2.0)]   // Frequent
    public void EventTrack_GetInterval_MapsFrequencyCorrectly(double freq, double expectedMin, double expectedMax)
    {
        var et = new EventTrackViewModel { Frequency = freq };
        var (min, max) = et.GetInterval();
        min.Should().Be(expectedMin);
        max.Should().Be(expectedMax);
    }

    [Fact]
    public void EventTrack_ToAnimationDefinition_ProducesValidDefinition()
    {
        var et = new EventTrackViewModel
        {
            Preset = EventPreset.CandleFlicker,
            Frequency = 0.5
        };

        var def = et.ToAnimationDefinition();

        def.Type.Should().Be(AnimationType.Event);
        def.LightAssignment.Should().Be(LightAssignment.Random);
        def.EventPattern.Should().NotBeNull();
        def.EventPattern!.Triggers.Should().NotBeEmpty();
        def.EventPattern.MinIntervalSeconds.Should().Be(3.0);
        def.EventPattern.MaxIntervalSeconds.Should().Be(6.0);
        def.EventPreset.Should().Be("CandleFlicker");
    }

    [Fact]
    public void EventTrack_PresetChanged_UpdatesDisplayName()
    {
        var et = new EventTrackViewModel { Preset = EventPreset.LightningFlash };
        et.DisplayName.Should().Be("Lightning Flash");

        et.Preset = EventPreset.CandleFlicker;
        et.DisplayName.Should().Be("Candle Flicker");
    }

    #endregion

    #region ExportToJson

    [Fact]
    public void ExportToJson_WithNoRoom_ReturnsFailure()
    {
        var result = _vm.ExportToJson();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ExportToJson_ProducesValidJson()
    {
        // Set up minimal state for export
        SetupRoomWithTracks();

        var result = _vm.ExportToJson();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("\"name\"");
        result.Value.Should().Contain("\"animations\"");
        result.Value.Should().Contain("\"keyframe\""); // AnimationType.Keyframe serialized as camelCase
    }

    #endregion

    #region Helpers

    private static TrackViewModel CreateTrackWithKeyframes(params double[] times)
    {
        return new TrackViewModel
        {
            LightId = Guid.NewGuid().ToString(),
            DisplayName = "Test Track",
            Keyframes = new ObservableCollection<KeyframeViewModel>(
                times.Select(t => new KeyframeViewModel
                {
                    TimeSeconds = t,
                    Color = HueColors.WarmWhite,
                    Brightness = 1.0,
                    Transition = TransitionStyle.EaseInOut
                })
            )
        };
    }

    private void SetupRoomWithTracks()
    {
        var room = new RoomModel
        {
            Id = Guid.NewGuid(),
            Name = "Test Room",
            Lights = new List<LightModel>
            {
                new() { Id = Guid.NewGuid(), Name = "Light 1" },
                new() { Id = Guid.NewGuid(), Name = "Light 2" },
            }
        };

        // Manually set the selected room field and add tracks
        // (avoiding OnSelectedRoomChanged which needs bridge service)
        _vm.Rooms.Add(room);
        foreach (var light in room.Lights)
        {
            _vm.Tracks.Add(new TrackViewModel
            {
                LightId = light.Id.ToString(),
                DisplayName = light.Name,
                Keyframes = new ObservableCollection<KeyframeViewModel>
                {
                    new() { TimeSeconds = 0, Color = HueColors.WarmWhite, Brightness = 1.0 },
                    new() { TimeSeconds = 8, Color = new HueColor(0.15, 0.06), Brightness = 0.5 },
                }
            });
        }

        // Set SelectedRoom directly so ExportToJson sees it
        _vm.SelectedRoom = room;
    }

    #endregion
}
