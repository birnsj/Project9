using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Project9
{
    /// <summary>
    /// Input action types
    /// </summary>
    public enum InputAction
    {
        None,
        Attack,
        MoveTo,
        DragFollow,
        ToggleSneak,
        ToggleGrid,
        ToggleCollision,
        ToggleCollisionSpheres,
        TogglePath,
        ToggleDiagnostics,
        ResetDiagnostics,
        ToggleLog,
        ReturnCamera,
        PanCamera,
        Zoom
    }

    /// <summary>
    /// Input event data
    /// </summary>
    public class InputEvent
    {
        public InputAction Action { get; set; }
        public Vector2 WorldPosition { get; set; }
        public Vector2 Direction { get; set; }
        public float ZoomDelta { get; set; }
        public Enemy? TargetEnemy { get; set; }
    }

    /// <summary>
    /// Manages all input handling (mouse and keyboard)
    /// </summary>
    public class InputManager
    {
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private bool _isDragging = false;
        private Vector2 _clickStartPos;
        private const float DRAG_THRESHOLD = 10.0f;

        private Camera _camera;
        private Func<Vector2, Vector2> _screenToWorld;

        public InputManager(Camera camera, Func<Vector2, Vector2> screenToWorld)
        {
            _camera = camera;
            _screenToWorld = screenToWorld;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

        /// <summary>
        /// Process all input and return events
        /// </summary>
        public InputEvent? ProcessInput(float deltaTime, Player player, System.Collections.Generic.List<Enemy> enemies)
        {
            KeyboardState currentKeyboardState = Keyboard.GetState();
            MouseState currentMouseState = Mouse.GetState();

            InputEvent? inputEvent = null;

            // Handle keyboard toggles
            if (currentKeyboardState.IsKeyDown(Keys.LeftControl) && 
                !_previousKeyboardState.IsKeyDown(Keys.LeftControl))
            {
                inputEvent = new InputEvent { Action = InputAction.ToggleSneak };
            }
            else if (currentKeyboardState.IsKeyDown(Keys.Space) && 
                     !_previousKeyboardState.IsKeyDown(Keys.Space))
            {
                inputEvent = new InputEvent { Action = InputAction.ReturnCamera };
            }
            else if (currentKeyboardState.IsKeyDown(Keys.G) && 
                     !_previousKeyboardState.IsKeyDown(Keys.G))
            {
                inputEvent = new InputEvent { Action = InputAction.ToggleGrid };
            }
            else if (currentKeyboardState.IsKeyDown(Keys.C) && 
                     !_previousKeyboardState.IsKeyDown(Keys.C))
            {
                inputEvent = new InputEvent { Action = InputAction.ToggleCollision };
            }
            else if (currentKeyboardState.IsKeyDown(Keys.V) && 
                     !_previousKeyboardState.IsKeyDown(Keys.V))
            {
                inputEvent = new InputEvent { Action = InputAction.ToggleCollisionSpheres };
            }
            else if (currentKeyboardState.IsKeyDown(Keys.P) && 
                     !_previousKeyboardState.IsKeyDown(Keys.P))
            {
                inputEvent = new InputEvent { Action = InputAction.TogglePath };
            }
            else if (currentKeyboardState.IsKeyDown(Keys.F3) && 
                     !_previousKeyboardState.IsKeyDown(Keys.F3))
            {
                inputEvent = new InputEvent { Action = InputAction.ToggleDiagnostics };
            }
            else if (currentKeyboardState.IsKeyDown(Keys.R) && 
                     !_previousKeyboardState.IsKeyDown(Keys.R))
            {
                inputEvent = new InputEvent { Action = InputAction.ResetDiagnostics };
            }
            else if (currentKeyboardState.IsKeyDown(Keys.L) && 
                     !_previousKeyboardState.IsKeyDown(Keys.L))
            {
                inputEvent = new InputEvent { Action = InputAction.ToggleLog };
            }

            // Handle mouse input FIRST - mouse clicks should have priority
            // Handle mouse input
            Vector2 mouseScreenPos = new Vector2(currentMouseState.X, currentMouseState.Y);
            Vector2 mouseWorldPos = _screenToWorld(mouseScreenPos);

            // Check if click is over UI elements (top-left corner where UI panel is)
            // UI panel is at (10, 10) with size 250x80, plus button below
            bool isOverUI = mouseScreenPos.X >= 10 && mouseScreenPos.X <= 260 && 
                           mouseScreenPos.Y >= 10 && mouseScreenPos.Y <= 130;

            // FIX: Detect clicks properly even when clicking rapidly
            // The issue was that if you click while the button was still "pressed" from a previous frame,
            // the state transition check would fail. We need to detect new clicks even if the button
            // was already pressed, as long as it wasn't being held/dragged.
            bool isNewClick = currentMouseState.LeftButton == ButtonState.Pressed && 
                             _previousMouseState.LeftButton == ButtonState.Released;
            
            // Also detect clicks when button is pressed but mouse moved significantly (new click location)
            // This handles rapid clicking while player is moving - each click to a new location should register
            bool isRapidClick = currentMouseState.LeftButton == ButtonState.Pressed && 
                               _previousMouseState.LeftButton == ButtonState.Pressed &&
                               !_isDragging &&
                               Vector2.Distance(mouseWorldPos, _clickStartPos) > DRAG_THRESHOLD * 0.5f; // Mouse moved = new click intent
            
            // Left mouse button clicked (new click or rapid click)
            if (isNewClick || isRapidClick)
            {
                // Ignore clicks on UI elements
                if (isOverUI)
                {
                    Console.WriteLine($"[InputManager] Click ignored - over UI at ({mouseScreenPos.X:F0}, {mouseScreenPos.Y:F0})");
                }
                else
                {
                    Console.WriteLine($"[InputManager] Mouse clicked at ({mouseWorldPos.X:F0}, {mouseWorldPos.Y:F0}) (new={isNewClick}, rapid={isRapidClick})");
                    
                    // Reset drag state on new click
                    _isDragging = false;
                    _clickStartPos = mouseWorldPos;
                    
                    // Check for enemy attack
                    Enemy? targetEnemy = FindAttackableEnemy(mouseWorldPos, player, enemies);
                    
                    if (targetEnemy != null)
                    {
                        inputEvent = new InputEvent 
                        { 
                            Action = InputAction.Attack,
                            TargetEnemy = targetEnemy,
                            WorldPosition = mouseWorldPos
                        };
                    }
                    else
                    {
                        inputEvent = new InputEvent 
                        { 
                            Action = InputAction.MoveTo,
                            WorldPosition = mouseWorldPos
                        };
                    }
                }
            }
            // Left mouse button held
            else if (currentMouseState.LeftButton == ButtonState.Pressed && 
                     _previousMouseState.LeftButton == ButtonState.Pressed)
            {
                float dragDistance = Vector2.Distance(mouseWorldPos, _clickStartPos);
                
                if (dragDistance > DRAG_THRESHOLD && !_isDragging)
                {
                    Console.WriteLine("[InputManager] Entered drag mode");
                    _isDragging = true;
                }

                if (_isDragging)
                {
                    inputEvent = new InputEvent
                    {
                        Action = InputAction.DragFollow,
                        WorldPosition = mouseWorldPos
                    };
                }
            }
            // Left mouse button released
            else if (currentMouseState.LeftButton == ButtonState.Released && 
                     _previousMouseState.LeftButton == ButtonState.Pressed)
            {
                if (_isDragging)
                {
                    Console.WriteLine("[InputManager] Drag ended");
                    _isDragging = false;
                    // Return a special event to clear player target
                    inputEvent = new InputEvent { Action = InputAction.None };
                }
                else
                {
                    Console.WriteLine("[InputManager] Click released (not dragging) - single click move should have been processed");
                    // Don't return an event - the MoveTo was already sent on click
                }
                
                // Reset click start position on release to ensure clean state
                _clickStartPos = mouseWorldPos;
            }

            // Handle WASD camera panning (only if no mouse event was processed)
            if (inputEvent == null)
            {
                Vector2 panDirection = Vector2.Zero;
                if (currentKeyboardState.IsKeyDown(Keys.W)) panDirection.Y -= 1;
                if (currentKeyboardState.IsKeyDown(Keys.S)) panDirection.Y += 1;
                if (currentKeyboardState.IsKeyDown(Keys.A)) panDirection.X -= 1;
                if (currentKeyboardState.IsKeyDown(Keys.D)) panDirection.X += 1;

                if (panDirection != Vector2.Zero)
                {
                    panDirection.Normalize();
                    inputEvent = new InputEvent 
                    { 
                        Action = InputAction.PanCamera,
                        Direction = panDirection
                    };
                }
            }

            // Handle mouse zoom (only if no other event was processed)
            if (inputEvent == null)
            {
                int scrollDelta = currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                if (scrollDelta != 0)
                {
                    inputEvent = new InputEvent
                    {
                        Action = InputAction.Zoom,
                        ZoomDelta = scrollDelta,
                        WorldPosition = _screenToWorld(new Vector2(currentMouseState.X, currentMouseState.Y))
                    };
                }
            }

            // Always update previous state at the end to ensure proper tracking
            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;

            return inputEvent;
        }

        /// <summary>
        /// Find enemy to attack at click position
        /// </summary>
        private Enemy? FindAttackableEnemy(Vector2 clickPos, Player player, System.Collections.Generic.List<Enemy> enemies)
        {
            const float playerAttackRange = 80.0f;
            const float clickRadius = 30.0f; // Reduced - must click closer to enemy

            foreach (var enemy in enemies)
            {
                // Skip flashing enemies
                if (enemy.IsFlashing)
                    continue;

                float distanceToEnemy = Vector2.Distance(player.Position, enemy.Position);
                float clickDistanceToEnemy = Vector2.Distance(clickPos, enemy.Position);
                float clickDistanceToPlayer = Vector2.Distance(clickPos, player.Position);

                // Only attack if:
                // 1. Enemy is in attack range AND click is close to enemy (not far away)
                // 2. OR click is very close to enemy position (direct click on enemy)
                bool isDirectClickOnEnemy = clickDistanceToEnemy <= clickRadius;
                bool isAttackInRange = distanceToEnemy <= playerAttackRange && clickDistanceToEnemy < clickDistanceToPlayer * 0.7f;

                if (isDirectClickOnEnemy || isAttackInRange)
                {
                    return enemy;
                }
            }

            return null;
        }

        /// <summary>
        /// Check if currently dragging
        /// </summary>
        public bool IsDragging => _isDragging;
    }
}

