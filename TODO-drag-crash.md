# Drag-to-Brightness Crash Investigation

## Status
Drag functionality is **temporarily disabled** to prevent crashes. Needs fixing.

## What We Know

### The Crash
- App crashes when user drags on a RoomCard to adjust brightness
- Crash happens immediately on drag, not after prolonged use
- Disabling `CardRoot_PointerPressed` (early return) prevents the crash
- No debug logs were captured, suggesting crash happens in framework/XAML layer

### What Was Tried (Did Not Fix)
1. **Thread marshalling** - Added `DispatcherQueue` checks to ensure UI updates on UI thread
2. **Null-safe dispatcher access** - Used `DispatcherQueue?.HasThreadAccess`
3. **Throttling** - Added 100ms throttle to limit bridge service calls during drag
4. **Reduced property change triggers** - Only update toggle color on IsOn change, not BackgroundColorRgb
5. **Added `_isLoaded` checks** - Prevent updates before control is fully loaded

### Code Flow During Drag
```
CardRoot_PointerPressed
  → CapturePointer(e.Pointer)
  → _isDragging = true

CardRoot_PointerMoved (on each mouse move)
  → Calculate new brightness
  → ViewModel.SetBrightnessCommand.Execute(newBrightness)
    → SetBrightnessAsync (async Task)
      → Brightness property set
      → OnPropertyChanged(BrightnessPercent)
      → await _bridgeService.SetRoomBrightnessAsync()
      → Possibly update IsOn state
```

### Suspect Areas
1. **Pointer capture** - `CardRoot.CapturePointer(e.Pointer)` might be failing
2. **Rapid async command execution** - `SetBrightnessCommand.Execute()` called many times
3. **XAML binding updates** - `BrightnessPercent` binding updating during pointer capture
4. **ProgressBar binding** - `{x:Bind ViewModel.BrightnessPercent}` during rapid updates

## What To Try Next

### 1. Isolate Pointer Capture
```csharp
// Try without pointer capture
private void CardRoot_PointerPressed(...)
{
    // Comment out: CardRoot.CapturePointer(e.Pointer);
    // See if crash still happens
}
```

### 2. Isolate Command Execution
```csharp
// Try without calling the command
if (totalDistance > DragThreshold)
{
    _hasMovedEnough = true;
    var newBrightness = Math.Clamp(...);
    // Comment out: ViewModel.SetBrightnessCommand.Execute(newBrightness);
    // Just log or update local state
}
```

### 3. Try Synchronous Brightness Update
Create a non-async method that just updates the UI without calling bridge:
```csharp
// In RoomCardViewModel
public void SetBrightnessLocal(double brightness)
{
    Brightness = brightness;
    OnPropertyChanged(nameof(BrightnessPercent));
    // Don't call bridge service
}
```

### 4. Check for Binding Errors
Run with debugger attached and check Output window for binding errors during drag.

### 5. Try Different Binding Mode
Change ProgressBar binding from OneWay to OneTime to prevent updates during drag:
```xaml
<ProgressBar Value="{x:Bind ViewModel.BrightnessPercent, Mode=OneTime}" .../>
```

### 6. Use PointerCanceled Event
Add handler for `PointerCanceled` to see if pointer is being cancelled unexpectedly.

## Files Modified
- `src/HueWindows/Controls/RoomCard.xaml.cs` - Drag disabled with early return
- `src/HueWindows/Controls/RoomCard.xaml` - No changes needed yet

## To Re-enable Drag
Remove the early `return;` statement in `CardRoot_PointerPressed`:
```csharp
private void CardRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
{
    // Temporarily disabled for debugging
    return;  // <-- REMOVE THIS LINE
    ...
}
```
