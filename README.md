# iOS Touch Event Bug Reproduction (MAUI 10.0.60)

This repository reproduces a severe regression bug introduced in **.NET MAUI 10.0.60** where touch events on elements inside a `ContentView` or `LayoutView` permanently stop working on iOS if the wrapper's opacity is animated and its parent's `IsEnabled` property is toggled, provided the wrapper does not have an explicit background color.

## Bug Overview
When navigating or loading states, developers often toggle a layout's visibility, disable it (`IsEnabled = false`), and fade views out to transparent (`Opacity = 0.0`). 

In .NET MAUI 10.0.60, if `IsEnabled` on a parent layout is toggled while a `ContentView` or `LayoutView` with no explicit background color is faded out, MAUI sets the native iOS view's background to `null`.

Once the sequence completes and the view is faded back in, the iOS `UIView` representing the `ContentView` remains completely transparent to touches (it fails hit testing). Touch event handlers on children (like `SKCanvasView`) are no longer triggered because their parent swallows or ignores the hit tests, breaking all user interaction with the control.

This bug does **not** occur in .NET MAUI **10.0.50**.

---

## The Missing Link: Why raw `SKCanvasView` works, but `ContentView` fails
If you call `FadeToAsync(0.0)` directly on a raw `SKCanvasView` that is placed directly in the layout, **the touch events will work correctly in 10.0.60**. 

This is because the MAUI 10.0.60 bug introduced in **PR #31340** has a very specific type check:
```csharp
// src/Core/src/Platform/iOS/ViewExtensions.cs
if (paint.IsNullOrEmpty())
{
    if (platformView is LayoutView or ContentView) // <--- CRITICAL CONDITION
        platformView.BackgroundColor = null;
    else
        return;
}
```
`SKCanvasView` translates natively to a `UIView`, which is neither a `LayoutView` nor a `ContentView` in MAUI's native hierarchy. Therefore, it bypasses the buggy assignment. 

To trigger the bug, the view being faded must be a `ContentView` or `LayoutView` (e.g. a custom control inheriting from `ContentView` that wraps the `SKCanvasView`).

---

## Steps to Reproduce
1. Run this application on an iOS Simulator or Device.
2. Tap the **"Trigger Fade Sequence"** button.
3. Wait for the sequence to complete.
4. Tap the red rectangle (the `SKCanvasView` wrapped in a `ContentView`).
5. Observe the result:
   - Under `.NET MAUI 10.0.50` (or if the bug is fixed), the label updates to `"Touch detected!"`.
   - Under `.NET MAUI 10.0.60`, nothing happens. The touch event handler never fires because the `ContentView` wrapper's native background is `null`.

---

## Proposed Fix
To fix this bug in .NET MAUI, the framework should avoid assigning `null` to the background color when a background paint is empty. Instead, it should assign `UIColor.Clear` which keeps it visually transparent while ensuring hit tests and touch events continue to function correctly.

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
