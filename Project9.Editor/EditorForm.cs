using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Project9.Shared;

namespace Project9.Editor
{
    public partial class EditorForm : Form
    {
        private MapRenderControl _mapRenderControl = null!;
        private ToolStrip _toolStrip = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _positionLabel = null!;
        private ToolStripStatusLabel _zoomLabel = null!;
        private MenuStrip _menuStrip = null!;
        private EditorMapData _mapData = null!;
        private TileTextureLoader _textureLoader = null!;
        private System.Windows.Forms.Timer _statusUpdateTimer = null!;
        private bool _collisionMode = false;
        private ToolStripButton? _collisionButton = null!;
        private TrackBar? _opacitySlider = null;

        public EditorForm()
        {
            InitializeComponent();
            InitializeEditor();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = "Tile Editor";
            this.Size = new Size(1200, 800);
            this.WindowState = FormWindowState.Maximized;

            // Menu Strip
            _menuStrip = new MenuStrip();
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            
            ToolStripMenuItem loadMenuItem = new ToolStripMenuItem("Load...");
            loadMenuItem.Click += LoadMenuItem_Click;
            fileMenu.DropDownItems.Add(loadMenuItem);

            ToolStripMenuItem saveMenuItem = new ToolStripMenuItem("Save");
            saveMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            saveMenuItem.Click += SaveMenuItem_Click;
            fileMenu.DropDownItems.Add(saveMenuItem);

            ToolStripMenuItem saveAsMenuItem = new ToolStripMenuItem("Save As...");
            saveAsMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            saveAsMenuItem.Click += SaveAsMenuItem_Click;
            fileMenu.DropDownItems.Add(saveAsMenuItem);

            _menuStrip.Items.Add(fileMenu);
            
            // About Menu
            ToolStripMenuItem aboutMenu = new ToolStripMenuItem("About");
            aboutMenu.Click += AboutMenu_Click;
            _menuStrip.Items.Add(aboutMenu);
            
            this.MainMenuStrip = _menuStrip;

            // Tool Strip for tile selection
            _toolStrip = new ToolStrip();
            _toolStrip.Dock = DockStyle.Top;
            
            // Add label
            ToolStripLabel label = new ToolStripLabel("Tile Type:");
            _toolStrip.Items.Add(label);

            // Add buttons for each terrain type (except Test, which gets its own button)
            foreach (TerrainType terrainType in Enum.GetValues<TerrainType>())
            {
                if (terrainType == TerrainType.Test)
                    continue; // Skip Test, add it separately below
                    
                ToolStripButton button = new ToolStripButton(terrainType.ToString());
                button.Tag = terrainType;
                button.Click += TileTypeButton_Click;
                button.DisplayStyle = ToolStripItemDisplayStyle.Text;
                _toolStrip.Items.Add(button);
            }

            // Add separator
            _toolStrip.Items.Add(new ToolStripSeparator());

            // Add dedicated Test Tile button (prominent)
            ToolStripButton testTileButton = new ToolStripButton("Test Tile")
            {
                Tag = TerrainType.Test,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                BackColor = Color.LightGreen // Make it stand out
            };
            testTileButton.Click += TileTypeButton_Click;
            _toolStrip.Items.Add(testTileButton);

            // Add separator
            _toolStrip.Items.Add(new ToolStripSeparator());

            // Add checkbox for 64x32 grid
            ToolStripControlHost gridCheckBoxHost = new ToolStripControlHost(new CheckBox
            {
                Text = "Show 64x32 Grid",
                AutoSize = true,
                Checked = false
            });
            ((CheckBox)gridCheckBoxHost.Control).CheckedChanged += ShowGridCheckBox_CheckedChanged;
            _toolStrip.Items.Add(gridCheckBoxHost);

            // Add separator
            _toolStrip.Items.Add(new ToolStripSeparator());

            // Add opacity slider
            ToolStripLabel opacityLabel = new ToolStripLabel("Tile Opacity:");
            _toolStrip.Items.Add(opacityLabel);

            _opacitySlider = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 70, // Default to 70% (0.7f)
                Width = 150,
                TickFrequency = 10,
                AutoSize = false
            };
            ToolStripControlHost opacitySliderHost = new ToolStripControlHost(_opacitySlider);
            _opacitySlider.ValueChanged += (sender, e) =>
            {
                if (_mapRenderControl != null)
                {
                    _mapRenderControl.TileOpacity = _opacitySlider.Value / 100.0f;
                }
            };
            _toolStrip.Items.Add(opacitySliderHost);

            // Add separator
            _toolStrip.Items.Add(new ToolStripSeparator());

            // Add collision mode button (toggle button)
            _collisionButton = new ToolStripButton("Collision Mode")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                CheckOnClick = true // Make it a toggle button
            };
            _collisionButton.Click += CollisionButton_Click;
            _toolStrip.Items.Add(_collisionButton);

            // Add delete all collision button
            var deleteAllCollisionButton = new ToolStripButton("Delete All Collision")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            deleteAllCollisionButton.Click += DeleteAllCollisionButton_Click;
            _toolStrip.Items.Add(deleteAllCollisionButton);

            // Map Render Control
            _mapRenderControl = new MapRenderControl();
            _mapRenderControl.Dock = DockStyle.Fill;
            _mapRenderControl.SelectedTerrainType = TerrainType.Grass;
            // Set initial opacity to match slider
            if (_opacitySlider != null)
            {
                _mapRenderControl.TileOpacity = _opacitySlider.Value / 100.0f;
            }

            // Status Strip
            _statusStrip = new StatusStrip();
            _positionLabel = new ToolStripStatusLabel("Position: (0, 0)");
            _zoomLabel = new ToolStripStatusLabel("Zoom: 1.0x");
            _statusStrip.Items.Add(_positionLabel);
            _statusStrip.Items.Add(_zoomLabel);

            // Layout
            this.Controls.Add(_mapRenderControl);
            this.Controls.Add(_toolStrip);
            this.Controls.Add(_statusStrip);
            this.Controls.Add(_menuStrip);

            // Update selected button
            UpdateSelectedTileButton(TerrainType.Grass);

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private async void InitializeEditor()
        {
            // Initialize map data and texture loader
            _mapData = new EditorMapData();
            _textureLoader = new TileTextureLoader();
            
            // Load textures
            _textureLoader.LoadTextures();
            
            // Load map
            await _mapData.LoadAsync();
            
            // Initialize map render control
            _mapRenderControl.Initialize(_mapData, _textureLoader);
            
            // Start status update timer
            _statusUpdateTimer = new System.Windows.Forms.Timer();
            _statusUpdateTimer.Interval = 100; // Update every 100ms
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();
        }

        private void StatusUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_mapRenderControl != null)
            {
                PointF pos = _mapRenderControl.Camera.Position;
                float zoom = _mapRenderControl.Camera.Zoom;
                _positionLabel.Text = $"Position: ({pos.X:F1}, {pos.Y:F1})";
                _zoomLabel.Text = $"Zoom: {zoom:F2}x";
            }
        }

        private void TileTypeButton_Click(object? sender, EventArgs e)
        {
            if (sender is ToolStripButton button && button.Tag is TerrainType terrainType)
            {
                _mapRenderControl.SelectedTerrainType = terrainType;
                UpdateSelectedTileButton(terrainType);
            }
        }

        private void UpdateSelectedTileButton(TerrainType selectedType)
        {
            foreach (ToolStripItem item in _toolStrip.Items)
            {
                if (item is ToolStripButton button && button.Tag is TerrainType terrainType)
                {
                    button.BackColor = terrainType == selectedType ? Color.LightBlue : Color.Transparent;
                }
            }
        }

        private async void LoadMenuItem_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                dialog.Title = "Load Map";
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        await _mapData.LoadAsync(dialog.FileName);
                        _mapRenderControl.Initialize(_mapData, _textureLoader);
                        MessageBox.Show("Map loaded successfully.", "Load Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading map: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async void SaveMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                await _mapData.SaveAsync();
                // Collision cells are saved automatically when placed/removed
                MessageBox.Show("Map saved successfully.", "Save Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving map: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void SaveAsMenuItem_Click(object? sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                dialog.Title = "Save Map As";
                dialog.FileName = "world.json";
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        await _mapData.SaveAsync(dialog.FileName);
                        MessageBox.Show("Map saved successfully.", "Save Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving map: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void AboutMenu_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("Project 9 V001", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowGridCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (_mapRenderControl != null && sender is CheckBox checkBox)
            {
                _mapRenderControl.ShowGrid64x32 = checkBox.Checked;
            }
        }

        private void CollisionButton_Click(object? sender, EventArgs e)
        {
            // The button's Checked state determines collision mode
            _collisionMode = _collisionButton?.Checked ?? false;
            if (_collisionButton != null)
            {
                _collisionButton.BackColor = _collisionMode ? Color.LightBlue : Color.Transparent;
            }
            if (_mapRenderControl != null)
            {
                _mapRenderControl.CollisionMode = _collisionMode;
            }
        }

        private void DeleteAllCollisionButton_Click(object? sender, EventArgs e)
        {
            // Confirm deletion
            DialogResult result = MessageBox.Show(
                "Are you sure you want to delete all collision cells? This action cannot be undone.",
                "Delete All Collision Cells",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                if (_mapRenderControl != null)
                {
                    _mapRenderControl.ClearAllCollisionCells();
                    MessageBox.Show("All collision cells have been deleted.", "Delete All Collision", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _statusUpdateTimer?.Stop();
                _statusUpdateTimer?.Dispose();
                _textureLoader?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

