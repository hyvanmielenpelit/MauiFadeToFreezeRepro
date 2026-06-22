using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace MauiFadeToFreezeRepro;

/// <summary>
/// MainPage demonstrating the .NET MAUI 10.0.60 iOS Touch Event Bug.
/// </summary>
public partial class MainPage : ContentPage
{
	private int _touchCount = 0;
	private FootballAnimation _football = new FootballAnimation();
	private IDispatcherTimer? _timer;
	private DateTime _lastUpdate;

	public MainPage()
	{
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_lastUpdate = DateTime.Now;
		
		_timer = Dispatcher.CreateTimer();
		_timer.Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0);
		_timer.Tick += (s, e) =>
		{
			MainCanvasView.InvalidateSurface();
		};
		_timer.Start();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_timer?.Stop();
	}

	private void OnCanvasPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
	{
		var canvas = e.Surface.Canvas;
		canvas.Clear(SKColors.DarkRed);

		var now = DateTime.Now;
		var dt = (now - _lastUpdate).TotalSeconds;
		_lastUpdate = now;

		// Draw the bouncing football
		_football.Update(dt, e.Info.Width, e.Info.Height);
		_football.Draw(canvas);

		// Keeping the interactive prompt text
		using var paint = new SKPaint
		{
			Color = SKColors.White,
			IsAntialias = true,
		};

		var coord = new SKPoint(e.Info.Width / 2f, e.Info.Height / 2f);
		canvas.DrawText("Tap Me!", coord, SKTextAlign.Center, new SKFont(SKTypeface.Default, 24), paint);
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
			// MainCanvasWrapper (ContentView).
			//
			// This invokes UpdateBackground in Microsoft.Maui.Platform.ViewExtensions (iOS).
			// Since the wrapper does not have an explicit MAUI background color, 
			// the new code introduced in MAUI PR #31340 (10.0.60) assigns:
			//     if (platformView is LayoutView or ContentView)
			//         platformView.BackgroundColor = null;
			//
			// When BackgroundColor becomes null, the native iOS view drops touches or 
			// becomes transparent to hit-testing, breaking the touch event handlers
			// permanently for everything inside it even after it fades back in.
			// =========================================================================
			MainGrid.IsEnabled = false;
			
			MainCanvasWrapper.Opacity = 1.0;

			StatusLabel.Text = "Fading out (FadeToAsync 0.0)...";
			await MainCanvasWrapper.FadeToAsync(0.0, 500);

			StatusLabel.Text = "Simulating load delay...";
			
			var delayTimer = Dispatcher.CreateTimer();
			delayTimer.Interval = TimeSpan.FromMilliseconds(1000);
			delayTimer.IsRepeating = false;
			delayTimer.Tick += async (s, ev) =>
			{
				try
				{
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
			};
			delayTimer.Start();
		}
		catch (Exception ex)
		{
			StatusLabel.Text = $"Error: {ex.Message}";
			StatusLabel.TextColor = Colors.Red;
			TriggerBtn.IsEnabled = true;
		}
	}
}
