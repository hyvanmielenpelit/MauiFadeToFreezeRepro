using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace MauiFadeToFreezeRepro;

/// <summary>
/// MainPage demonstrating the .NET MAUI 10.0.60 iOS Touch Event Bug.
/// </summary>
public partial class MainPage : ContentPage
{
	private int _touchCount = 0;

	public MainPage()
	{
		InitializeComponent();
	}

	private void OnCanvasPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
	{
		var canvas = e.Surface.Canvas;
		canvas.Clear(SKColors.DarkRed);

		using var paint = new SKPaint
		{
			Color = SKColors.White,
			IsAntialias = true,
			TextSize = 24,
			TextAlign = SKTextAlign.Center
		};

		var coord = new SKPoint(e.Info.Width / 2f, e.Info.Height / 2f);
		canvas.DrawText("Tap Me!", coord, paint);
	}

	private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
	{
		if (e.ActionType == SKTouchAction.Pressed)
		{
			_touchCount++;
			StatusLabel.Text = $"Touch detected! Count: {_touchCount}";
			StatusLabel.TextColor = Colors.LimeGreen;
		}
		
		e.Handled = true;
	}

	/// <summary>
	/// Triggers the animation sequence that reproduces the bug.
	/// </summary>
	private async void OnTriggerBugClicked(object? sender, EventArgs e)
	{
		TriggerBtn.IsEnabled = false;
		StatusLabel.Text = "Running fade sequence...";
		StatusLabel.TextColor = Colors.Yellow;
		_touchCount = 0;
		
		try
		{
			// =========================================================================
			// BUG TRIGGER IN 10.0.60:
			// Setting IsEnabled = false triggers the Visual State Manager (VSM) to 
			// update the visual states of all descendent controls, including the 
			// MainCanvasView (SKCanvasView).
			//
			// This invokes UpdateBackground in Microsoft.Maui.Platform.ViewExtensions (iOS).
			// Since our SKCanvasView does not have an explicit MAUI background color, 
			// the new code introduced in MAUI PR #31340 (10.0.60) assigns:
			//     platformView.BackgroundColor = null;
			//
			// When BackgroundColor becomes null, the native iOS view drops touches or 
			// becomes transparent to touches, breaking the SKCanvasView touch event handlers
			// permanently even after it fades back in.
			// =========================================================================
			MainGrid.IsEnabled = false;
			
			MainCanvasWrapper.Opacity = 1.0;

			StatusLabel.Text = "Fading out (FadeToAsync 0.0)...";
			await MainCanvasWrapper.FadeToAsync(0.0, 500);

			StatusLabel.Text = "Simulating load delay...";
			await Task.Delay(1000);

			MainGrid.IsEnabled = true;
			
			StatusLabel.Text = "Fading in (FadeToAsync 1.0)...";
			MainCanvasWrapper.Opacity = 0.0;
			await MainCanvasWrapper.FadeToAsync(1.0, 500);

			StatusLabel.Text = "Sequence completed! Now tap the red square.";
		}
		catch (Exception ex)
		{
			StatusLabel.Text = $"Error: {ex.Message}";
			StatusLabel.TextColor = Colors.Red;
		}
		finally
		{
			TriggerBtn.IsEnabled = true;
		}
	}
}
