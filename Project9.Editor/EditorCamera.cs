using System;
using System.Numerics;

namespace Project9.Editor;

public class EditorCamera
{
    private Vector2 _position;
    private float _zoom;
    private float _minZoom;
    private float _maxZoom;
    private float _panSpeed;

    public Vector2 Position
    {
        get => _position;
        set => _position = value;
    }

    public float Zoom
    {
        get => _zoom;
        set => _zoom = Math.Clamp(value, _minZoom, _maxZoom);
    }

    public EditorCamera()
    {
        _position = Vector2.Zero;
        _zoom = 1.0f;
        _minZoom = 0.5f;
        _maxZoom = 4.0f;
        _panSpeed = 800.0f;
    }

    public void Pan(Vector2 direction, float deltaTime)
    {
        if (direction.LengthSquared() > 0)
        {
            direction = Vector2.Normalize(direction);
            _position += direction * _panSpeed * deltaTime;
        }
    }

    public void ZoomIn(float amount)
    {
        Zoom += amount;
    }

    public void ZoomOut(float amount)
    {
        Zoom -= amount;
    }

    public Matrix3x2 GetTransform()
    {
        // Create transform: translate, then scale
        return Matrix3x2.CreateTranslation(-_position.X, -_position.Y) *
               Matrix3x2.CreateScale(_zoom, _zoom);
    }
}



