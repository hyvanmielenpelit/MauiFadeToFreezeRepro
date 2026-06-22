# iOS FadeToAsync Freeze Reproduction (MAUI 10.0.60)

This repository reproduces a severe regression bug introduced in **.NET MAUI 10.0.60** where awaiting `FadeToAsync` on iOS freezes indefinitely (never completes) if the target view does not have an explicit background color and its opacity is set/animated to `0.0`.

## Bug Overview
When navigating or loading states, developers often toggle a layout's visibility, disable it (`IsEnabled = false`), and fade views out to transparent (`Opacity = 0`). In .NET MAUI 10.0.60, if `IsEnabled` on a parent layout is toggled while an `SKCanvasView` (or other `LayoutView` / `ContentView`) with no explicit background color is faded out, the task returned by `FadeToAsync` (and any wrapping `Task.WhenAll` or `await` statements) will **hang forever**.

This bug does **not** occur in .NET MAUI **10.0.50**.

---

## Steps to Reproduce
1. Run this application on an iOS Simulator or Device.
2. Tap the **"Trigger Fade Sequence"** button.
3. Observe the logs / UI:
   - Under `.NET MAUI 10.0.50`, the fade animations complete successfully and the status text displays `"Sequence Completed Successfully!"`.
   - Under `.NET MAUI 10.0.60`, the fade-out completes, but the task never returns. The app hangs at `"Fading out..."` or during the fade-in step, and the completion callback is never invoked.

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

When an iOS `UIView` has `BackgroundColor = null` and its `Opacity` is animated or set to `0.0`, iOS CoreAnimation completely drops the view's underlying `CALayer` from the active render tree since it is fully transparent and has no color representation.

Because the layer is dropped:
1. The `CADisplayLink` (used by MAUI's `AnimationManager` / `Tweener` on iOS) stops ticking for this view.
2. The animation never reaches its final tick.
3. The `TaskCompletionSource` associated with the `FadeToAsync` animation is never resolved, resulting in an infinite await/freeze.

---

## Proposed Fix
To fix this bug in .NET MAUI, we should avoid assigning `null` to the background color when a background paint is empty. Instead, we should assign `UIColor.Clear` which preserves the layer's presence in CoreAnimation while keeping it visually transparent.

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
