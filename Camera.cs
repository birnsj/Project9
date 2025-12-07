using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    public class Camera
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
            set => _zoom = MathHelper.Clamp(value, _minZoom, _maxZoom);
        }

        public Camera()
        {
            _position = Vector2.Zero;
            _zoom = 1.0f;
            _minZoom = 0.5f;
            _maxZoom = 4.0f;
            _panSpeed = 300.0f;
        }

        public Matrix GetTransform()
        {
            return Matrix.CreateTranslation(new Vector3(-_position.X, -_position.Y, 0)) *
                   Matrix.CreateScale(_zoom, _zoom, 1) *
                   Matrix.CreateTranslation(new Vector3(0, 0, 0));
        }

        public void Pan(Vector2 direction, float deltaTime)
        {
            if (direction.LengthSquared() > 0)
            {
                direction.Normalize();
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
    }
}
