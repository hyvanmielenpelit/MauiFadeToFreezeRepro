using SkiaSharp;
using System;

namespace MauiFadeToFreezeRepro;

/// <summary>
/// A minimal physics simulation for a bouncing football.
/// Kept in a separate file to avoid cluttering the main bug reproduction code.
/// </summary>
public class FootballAnimation
{
    private float _x, _y;
    private float _vx, _vy;
    private float _radius = 20f;
    private float _rotation = 0f;

    public FootballAnimation()
    {
        // Initial velocity (pixels per second)
        _vx = 120f;
        _vy = 180f;
    }

    /// <summary>
    /// Updates the position and rotation of the football based on physics rules.
    /// </summary>
    public void Update(double deltaTime, float canvasWidth, float canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        // Initialize position if it's the first run
        if (_x == 0 && _y == 0)
        {
            _x = canvasWidth / 2;
            _y = canvasHeight / 2;
        }

        // Apply movement
        _x += _vx * (float)deltaTime;
        _y += _vy * (float)deltaTime;

        // Apply rotation (rolling effect)
        float speed = (float)Math.Sqrt(_vx * _vx + _vy * _vy);
        _rotation += (speed * (float)deltaTime) / _radius * (180f / (float)Math.PI);

        // Bouncing logic
        if (_x - _radius < 0)
        {
            _x = _radius;
            _vx = -_vx;
        }
        else if (_x + _radius > canvasWidth)
        {
            _x = canvasWidth - _radius;
            _vx = -_vx;
        }

        if (_y - _radius < 0)
        {
            _y = _radius;
            _vy = -_vy;
        }
        else if (_y + _radius > canvasHeight)
        {
            _y = canvasHeight - _radius;
            _vy = -_vy;
        }
    }

    /// <summary>
    /// Draws the football to the canvas.
    /// </summary>
    public void Draw(SKCanvas canvas)
    {
        if (_x == 0 && _y == 0) return;

        canvas.Save();
        canvas.Translate(_x, _y);
        canvas.RotateDegrees(_rotation);

        // Draw the football base (white circle)
        using var fillPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(0, 0, _radius, fillPaint);

        // Draw the football pattern (simple lines)
        using var strokePaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3
        };
        
        canvas.DrawCircle(0, 0, _radius, strokePaint);
        
        // Classic football pentagon/hexagon approximation lines
        canvas.DrawLine(-_radius, 0, _radius, 0, strokePaint);
        canvas.DrawLine(0, -_radius, 0, _radius, strokePaint);
        
        // Inner circle
        canvas.DrawCircle(0, 0, _radius / 2, strokePaint);

        canvas.Restore();
    }
}
