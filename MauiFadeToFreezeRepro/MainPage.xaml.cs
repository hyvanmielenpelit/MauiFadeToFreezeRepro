using System;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using Microsoft.Maui.Controls;

namespace MauiFadeToFreezeRepro;

/// <summary>
/// MainPage demonstrating the .NET MAUI 10.0.60 iOS Touch Event Bug using a GamePage replica layout.
/// </summary>
public partial class MainPage : ContentPage
{
	private int _touchCount = 0;
	private FootballAnimation _football = new FootballAnimation();
	private IDispatcherTimer? _animationTimer;
	private DateTime _lastUpdate;

	public MainPage()
	{
		InitializeComponent();
		
		var assembly = typeof(Microsoft.Maui.Controls.View).Assembly;
		var versionAttr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly);
		MauiVersionLabel.Text = $"MAUI Version: {versionAttr?.InformationalVersion ?? assembly.GetName().Version?.ToString()} (Expected: 10.0.91)";
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_lastUpdate = DateTime.Now;
		
		// Invalidate canvas at 60 FPS
		_animationTimer = Dispatcher.CreateTimer();
		_animationTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0);
		_animationTimer.Tick += (s, e) =>
		{
			InternalCanvas.InvalidateSurface();
		};
		_animationTimer.Start();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_animationTimer?.Stop();
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

		// Interactive prompt text
		using var paint = new SKPaint
		{
			Color = SKColors.White,
			IsAntialias = true,
		};

		var coord = new SKPoint(e.Info.Width / 2f, e.Info.Height / 2f);
		canvas.DrawText("Tap Football!", coord, SKTextAlign.Center, new SKFont(SKTypeface.Default, 24), paint);
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
	/// Triggers the fade sequence mimicking GnollHack showing a menu.
	/// </summary>
	private async void OnTriggerBugClicked(object? sender, EventArgs e)
	{
		TriggerBtn.IsEnabled = false;
		StatusLabel.Text = "Opening Menu (MainGrid disabled)...";
		StatusLabel.TextColor = Colors.Yellow;
		_touchCount = 0;
		
		try
		{
			// =========================================================================
			// BUG TRIGGER IN 10.0.60:
			// Setting MainGrid.IsEnabled = false propagates down the visual tree.
			// It updates the visual states of descendent controls, including the 
			// MainCanvasView (ContentView).
			//
			// This invokes UpdateBackground in Microsoft.Maui.Platform.ViewExtensions.
			// Since MainCanvasView (ContentView) has no explicit BackgroundColor set in MAUI, 
			// the 10.0.60 code sets its platform view's BackgroundColor to null.
			//
			// This makes the iOS view transparent to hit testing, causing iOS to drop 
			// subsequent touch events even after MainGrid is re-enabled.
			// =========================================================================
			MainGrid.IsEnabled = false;
			
			// Show Menu overlay (mimicking GnollHack MenuGrid)
			MenuGrid.Opacity = 0.0;
			MenuGrid.IsVisible = true;
			await MenuGrid.FadeTo(1.0, 250);

			StatusLabel.Text = "Menu visible. Simulating delayed closing (1 sec)...";

			// Delay using non-blocking dispatcher timer
			var closeTimer = Dispatcher.CreateTimer();
			closeTimer.Interval = TimeSpan.FromMilliseconds(1000);
			closeTimer.IsRepeating = false;
			closeTimer.Tick += async (s, ev) =>
			{
				try
				{
					StatusLabel.Text = "Fading out MenuGrid...";

                    // Re-enable MainGrid
                    MainGrid.IsEnabled = true;
                    await MenuGrid.FadeTo(0.0, 250);
					MenuGrid.IsVisible = false;

					StatusLabel.Text = "Sequence completed! Try tapping the football.";
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
			closeTimer.Start();
		}
		catch (Exception ex)
		{
			StatusLabel.Text = $"Error: {ex.Message}";
			StatusLabel.TextColor = Colors.Red;
			TriggerBtn.IsEnabled = true;
		}
	}

	// Placeholders for GnollHack GamePage emulation events

	private void GameMenuButton_Clicked(object? sender, EventArgs e)
	{
		// Mock showing popup dialog
		PopupLabel.Text = "Game Menu Button Clicked!";
		PopupGrid.IsVisible = true;
	}

	private void ESCButton_Clicked(object? sender, EventArgs e)
	{
		// Mock exit question dialog
		YnGrid.IsVisible = true;
	}

	private void GHButton_Clicked(object? sender, EventArgs e)
	{
		if (sender is Button btn)
		{
			StatusLabel.Text = $"{btn.Text} action triggered.";
			StatusLabel.TextColor = Colors.LightBlue;
		}
	}

	private void MenuOKButton_Clicked(object? sender, EventArgs e)
	{
		MenuGrid.IsVisible = false;
		MainGrid.IsEnabled = true;
	}

	private void PopupOkButton_Clicked(object? sender, EventArgs e)
	{
		PopupGrid.IsVisible = false;
	}

	private void YnButton_Clicked(object? sender, EventArgs e)
	{
		YnGrid.IsVisible = false;
	}
}
