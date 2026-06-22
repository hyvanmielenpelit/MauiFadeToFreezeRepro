namespace MauiFadeToFreezeRepro;

/// <summary>
/// MainPage demonstrating the .NET MAUI 10.0.60 iOS FadeToAsync freeze.
/// </summary>
public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	/// <summary>
	/// Triggers the animation sequence that reproduces the freeze on iOS.
	/// </summary>
	private async void OnTriggerBugClicked(object? sender, EventArgs e)
	{
		TriggerBtn.IsEnabled = false;
		StatusLabel.Text = "Running fade sequence...";
		
		try
		{
			// =========================================================================
			// STEP 1: Disable the parent grid.
			//
			// EXPLANATION:
			// In GnollHack (GamePage.xaml.cs), MainGrid.IsEnabled is set to false to
			// prevent user interactions during level transitions/fade animations.
			//
			// BUG TRIGGER IN 10.0.60:
			// Setting IsEnabled = false triggers the Visual State Manager (VSM) to 
			// update the visual states of all descendent controls, including the 
			// MainCanvasView (SKCanvasView).
			//
			// This invokes UpdateBackground in Microsoft.Maui.Platform.ViewExtensions (iOS).
			// Since our SKCanvasView does not have an explicit background color/paint, 
			// the new code introduced in MAUI PR #31340 (10.0.60) assigns:
			//     platformView.BackgroundColor = null;
			// =========================================================================
			MainGrid.IsEnabled = false;
			
			// Ensure opacity is starting at full visibility.
			MainCanvasView.Opacity = 1.0;

			// =========================================================================
			// STEP 2: Fade the SKCanvasView to 0.0 (completely transparent).
			//
			// EXPLANATION:
			// At the end of this animation, the SKCanvasView's Opacity becomes 0.0.
			//
			// BUG TRIGGER IN 10.0.60:
			// With a native BackgroundColor of null (from Step 1) AND an Opacity of 0.0,
			// iOS CoreAnimation considers the layer completely empty and transparent, 
			// and aggressively drops the CALayer from the active rendering pipeline.
			//
			// Because the layer is dropped, the native CADisplayLink ticks associated 
			// with this layer cease. MAUI's AnimationManager / Tweener relies on these 
			// display link ticks to progress and complete the FadeToAsync task.
			//
			// Without ticks, the TaskCompletionSource representing the animation remains 
			// unresolved, causing this await statement to HANG FOREVER.
			// =========================================================================
			StatusLabel.Text = "Fading out (FadeToAsync 0.0)...";
			await MainCanvasView.FadeToAsync(0.0, 500);

			// =========================================================================
			// STEP 3: Simulate level loading/transition delay.
			// (If we reach here, the fade-out completed. Sometimes the freeze happens on 
			// fade-out, sometimes on fade-in when IsEnabled is toggled back.)
			// =========================================================================
			StatusLabel.Text = "Simulating load delay...";
			await Task.Delay(1000);

			// =========================================================================
			// STEP 4: Re-enable the parent grid.
			//
			// EXPLANATION:
			// Toggles IsEnabled = true, which again triggers the VSM and re-runs
			// the background mapper, setting BackgroundColor = null on the transparent view.
			// =========================================================================
			MainGrid.IsEnabled = true;
			
			// =========================================================================
			// STEP 5: Fade the SKCanvasView back in to 1.0.
			//
			// EXPLANATION:
			// If the fade-out did not lock up, fading in from Opacity = 0.0 will freeze 
			// here because the layer was dropped during the fully transparent state, 
			// and CADisplayLink is inactive, so the fade-in Task will hang indefinitely.
			// =========================================================================
			StatusLabel.Text = "Fading in (FadeToAsync 1.0)...";
			await MainCanvasView.FadeToAsync(1.0, 500);

			StatusLabel.Text = "Sequence Completed Successfully!";
			await DisplayAlertAsync("Success", "The animation completed successfully. This is .NET MAUI 10.0.50 (or bug is fixed).", "OK");
		}
		catch (Exception ex)
		{
			StatusLabel.Text = $"Error: {ex.Message}";
		}
		finally
		{
			TriggerBtn.IsEnabled = true;
		}
	}
}
