using Microsoft.Xna.Framework;
using System;

namespace Project9
{
    /// <summary>
    /// Represents a floating damage number displayed above an entity
    /// </summary>
    public class DamageNumber
    {
        public Vector2 Position { get; set; }
        public float Damage { get; set; }
        public float Timer { get; set; }
        public float Lifetime { get; set; }
        public Vector2 Velocity { get; set; }
        
        public DamageNumber(Vector2 position, float damage)
        {
            Position = position;
            Damage = damage;
            Timer = 0.0f;
            Lifetime = GameConfig.DamageNumberLifetime;
            // Random upward velocity with slight horizontal drift
            Velocity = new Vector2(
                (float)(System.Random.Shared.NextDouble() * 20.0 - 10.0), // -10 to 10 horizontal
                -30.0f - (float)(System.Random.Shared.NextDouble() * 20.0) // -30 to -50 upward
            );
        }
        
        public void Update(float deltaTime)
        {
            Timer += deltaTime;
            Position += Velocity * deltaTime;
            // Slow down over time
            Velocity *= 0.95f;
        }
        
        public bool IsExpired => Timer >= Lifetime;
        
        public float Alpha
        {
            get
            {
                if (Timer < 0.2f)
                    return 1.0f; // Fade in quickly
                if (Timer > Lifetime * 0.7f)
                {
                    float fadeOut = 1.0f - ((Timer - Lifetime * 0.7f) / (Lifetime * 0.3f)); // Fade out in last 30%
                    return System.Math.Max(0.0f, fadeOut); // Ensure non-negative
                }
                return 1.0f;
            }
        }
    }
}

