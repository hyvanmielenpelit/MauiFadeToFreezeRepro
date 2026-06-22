# iOS Touch Event Bug Reproduction (MAUI 10.0.60)

This repository reproduces a severe regression bug introduced in **.NET MAUI 10.0.60** where touch events on an `SKCanvasView` (and potentially other layout views) permanently stop working on iOS if its parent's `IsEnabled` property is toggled while its opacity is animated or manipulated, and the view does not have an explicit background color.

## Bug Overview
When navigating or loading states, developers often toggle a layout's visibility, disable it (`IsEnabled = false`), and fade views out to transparent (`Opacity = 0`). In .NET MAUI 10.0.60, if `IsEnabled` on a parent layout is toggled while an `SKCanvasView` with no explicit background color is faded out, the native view's background gets set to `null`. 

Once the sequence completes and the view is faded back in, the iOS `UIView` remains completely transparent to touches. Touch event handlers (`OnCanvasTouch`) are no longer triggered, breaking all user interaction with the control.

This bug does **not** occur in .NET MAUI **10.0.50**.

---

## Steps to Reproduce
1. Run this application on an iOS Simulator or Device.
2. Tap the **"Trigger Fade Sequence"** button.
3. Wait for the sequence to complete.
4. Tap the red rectangle (the `SKCanvasView`).
5. Observe the result:
   - Under `.NET MAUI 10.0.50` (or if the bug is fixed), the label updates to `"Touch detected!"`.
   - Under `.NET MAUI 10.0.60`, nothing happens. The touch event handler never fires because the view's native background is `null`.

---

## Root Cause Analysis
The issue was introduced in .NET MAUI 10.0.60 by **PR #31340** (`f34ed80e87`), which modified how view backgrounds are cleared on iOS.

Specifically, in `src/Core/src/Platform/iOS/ViewExtensions.cs`, if the background `Paint` is null or empty, MAUI now sets the native iOS view's background color to `null` if it is a `LayoutView` or `ContentView` (which `SKCanvasViewRenderer` wraps or inherits from):

```csharp
// src/Core/src/Platform/iOS/ViewExtensions.cs
if (paint.IsNullOrEmpty())
{
    if (platformView is LayoutView or ContentView)
        platformView.BackgroundColor = null; // <-- Toggled here when IsEnabled changes and triggers VSM property updates
    else
        return;
}
```

When an iOS `UIView` has `BackgroundColor = null`, it becomes structurally transparent to touches. Even after `Opacity` is animated back to `1.0` and `IsEnabled` is set back to `true`, the `BackgroundColor` remains `null`, preventing the view from receiving hit tests and touch events.

---

## Proposed Fix
To fix this bug in .NET MAUI, we should avoid assigning `null` to the background color when a background paint is empty. Instead, we should assign `UIColor.Clear` which keeps it visually transparent while ensuring hit tests and touch events continue to function.

### Framework Diff:

```diff
diff --git a/src/Core/src/Platform/iOS/ViewExtensions.cs b/src/Core/src/Platform/iOS/ViewExtensions.cs
index a5b1c2b..d3fe90a 100644
--- a/src/Core/src/Platform/iOS/ViewExtensions.cs
+++ b/src/Core/src/Platform/iOS/ViewExtensions.cs
@@ -84,7 +84,7 @@ namespace Microsoft.Maui.Platform
  			if (paint.IsNullOrEmpty())
  			{
  				if (platformView is LayoutView or ContentView)
-					platformView.BackgroundColor = null;
+					platformView.BackgroundColor = UIColor.Clear;
  				else
  					return;
  			}
```
