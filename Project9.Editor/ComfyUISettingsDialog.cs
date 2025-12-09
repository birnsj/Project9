using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Project9.Editor
{
    public partial class ComfyUISettingsDialog : Form
    {
        private Label _serverUrlLabel = null!;
        private TextBox _serverUrlTextBox = null!;
        private Label _comfyUIPythonLabel = null!;
        private TextBox _comfyUIPythonTextBox = null!;
        private Button _browsePythonButton = null!;
        private Label _comfyUIInstallLabel = null!;
        private TextBox _comfyUIInstallTextBox = null!;
        private Button _browseInstallButton = null!;
        private CheckBox _autoStartCheckBox = null!;
        private CheckBox _rememberPathsCheckBox = null!;
        private Button _okButton = null!;
        private Button _cancelButton = null!;
        private GroupBox _pathsGroupBox = null!;
        private Label _lastWorkflowLabel = null!;
        private TextBox _lastWorkflowTextBox = null!;
        private Button _browseWorkflowButton = null!;
        private Label _lastOutputLabel = null!;
        private TextBox _lastOutputTextBox = null!;
        private Button _browseOutputButton = null!;
        private GroupBox _connectionGroupBox = null!;
        private Label _connectionStatusLabel = null!;
        private Button _checkConnectionButton = null!;
        private Button _terminateButton = null!;
        private Button _reconnectButton = null!;
        private Button _openBrowserButton = null!;
        private Button _loadWorkflowButton = null!;
        private ComfyUIServerManager? _serverManager;

        public ComfyUISettings Settings { get; private set; }

        public ComfyUISettingsDialog(ComfyUISettings settings)
        {
            Settings = settings;
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "ComfyUI Settings";
            this.Size = new Size(600, 620);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int yPos = 20;

            // Server URL
            _serverUrlLabel = new Label
            {
                Text = "ComfyUI Server URL:",
                Location = new Point(20, yPos),
                Size = new Size(150, 20),
                AutoSize = false
            };

            _serverUrlTextBox = new TextBox
            {
                Location = new Point(180, yPos - 2),
                Size = new Size(380, 23),
                Text = "http://localhost:8188"
            };

            yPos += 35;

            // ComfyUI Python Path
            _comfyUIPythonLabel = new Label
            {
                Text = "Python Executable:",
                Location = new Point(20, yPos),
                Size = new Size(150, 20),
                AutoSize = false
            };

            _comfyUIPythonTextBox = new TextBox
            {
                Location = new Point(180, yPos - 2),
                Size = new Size(290, 23)
            };

            _browsePythonButton = new Button
            {
                Text = "Browse...",
                Location = new Point(480, yPos - 2),
                Size = new Size(80, 25)
            };
            _browsePythonButton.Click += BrowsePythonButton_Click;

            yPos += 35;

            // ComfyUI Install Path
            _comfyUIInstallLabel = new Label
            {
                Text = "ComfyUI Install Path:",
                Location = new Point(20, yPos),
                Size = new Size(150, 20),
                AutoSize = false
            };

            _comfyUIInstallTextBox = new TextBox
            {
                Location = new Point(180, yPos - 2),
                Size = new Size(290, 23)
            };

            _browseInstallButton = new Button
            {
                Text = "Browse...",
                Location = new Point(480, yPos - 2),
                Size = new Size(80, 25)
            };
            _browseInstallButton.Click += BrowseInstallButton_Click;

            yPos += 35;

            // Connection Status Group Box
            _connectionGroupBox = new GroupBox
            {
                Text = "Connection Status",
                Location = new Point(20, yPos),
                Size = new Size(540, 140)
            };

            int connY = 25;

            _connectionStatusLabel = new Label
            {
                Text = "Status: Not checked",
                Location = new Point(10, connY),
                Size = new Size(400, 20),
                AutoSize = false
            };

            _checkConnectionButton = new Button
            {
                Text = "Check Connection",
                Location = new Point(10, connY + 25),
                Size = new Size(120, 25)
            };
            _checkConnectionButton.Click += CheckConnectionButton_Click;

            _terminateButton = new Button
            {
                Text = "Terminate",
                Location = new Point(140, connY + 25),
                Size = new Size(120, 25)
            };
            _terminateButton.Click += TerminateButton_Click;

            _reconnectButton = new Button
            {
                Text = "Reconnect",
                Location = new Point(270, connY + 25),
                Size = new Size(120, 25)
            };
            _reconnectButton.Click += ReconnectButton_Click;

            _openBrowserButton = new Button
            {
                Text = "Open ComfyUI",
                Location = new Point(400, connY + 25),
                Size = new Size(120, 25)
            };
            _openBrowserButton.Click += OpenBrowserButton_Click;

            _loadWorkflowButton = new Button
            {
                Text = "Load Workflow",
                Location = new Point(10, connY + 55),
                Size = new Size(200, 30)
            };
            _loadWorkflowButton.Click += LoadWorkflowButton_Click;

            _connectionGroupBox.Controls.Add(_connectionStatusLabel);
            _connectionGroupBox.Controls.Add(_checkConnectionButton);
            _connectionGroupBox.Controls.Add(_terminateButton);
            _connectionGroupBox.Controls.Add(_reconnectButton);
            _connectionGroupBox.Controls.Add(_openBrowserButton);
            _connectionGroupBox.Controls.Add(_loadWorkflowButton);

            yPos += 140;

            // Auto-start Checkbox
            _autoStartCheckBox = new CheckBox
            {
                Text = "Automatically start ComfyUI if not running",
                Location = new Point(20, yPos),
                Size = new Size(540, 20),
                Checked = false
            };

            yPos += 35;

            // Remember Paths Checkbox
            _rememberPathsCheckBox = new CheckBox
            {
                Text = "Remember last used workflow and output paths",
                Location = new Point(20, yPos),
                Size = new Size(540, 20),
                Checked = true
            };

            yPos += 35;

            // Paths Group Box
            _pathsGroupBox = new GroupBox
            {
                Text = "Saved Paths",
                Location = new Point(20, yPos),
                Size = new Size(540, 140),
                Enabled = true
            };

            int groupY = 25;

            // Last Workflow Path
            _lastWorkflowLabel = new Label
            {
                Text = "Last Workflow:",
                Location = new Point(10, groupY),
                Size = new Size(100, 20),
                AutoSize = false
            };

            _lastWorkflowTextBox = new TextBox
            {
                Location = new Point(10, groupY + 20),
                Size = new Size(420, 23),
                ReadOnly = true
            };

            _browseWorkflowButton = new Button
            {
                Text = "Load Workflow",
                Location = new Point(440, groupY + 19),
                Size = new Size(100, 25)
            };
            _browseWorkflowButton.Click += LoadWorkflowFromPathButton_Click;

            groupY += 60;

            // Last Output Directory
            _lastOutputLabel = new Label
            {
                Text = "Last Output:",
                Location = new Point(10, groupY),
                Size = new Size(100, 20),
                AutoSize = false
            };

            _lastOutputTextBox = new TextBox
            {
                Location = new Point(10, groupY + 20),
                Size = new Size(420, 23),
                ReadOnly = true
            };

            _browseOutputButton = new Button
            {
                Text = "Browse...",
                Location = new Point(440, groupY + 19),
                Size = new Size(85, 25)
            };
            _browseOutputButton.Click += BrowseOutputButton_Click;

            _pathsGroupBox.Controls.Add(_lastWorkflowLabel);
            _pathsGroupBox.Controls.Add(_lastWorkflowTextBox);
            _pathsGroupBox.Controls.Add(_browseWorkflowButton);
            _pathsGroupBox.Controls.Add(_lastOutputLabel);
            _pathsGroupBox.Controls.Add(_lastOutputTextBox);
            _pathsGroupBox.Controls.Add(_browseOutputButton);

            yPos += 150;

            // Buttons
            _okButton = new Button
            {
                Text = "OK",
                Location = new Point(440, yPos + 10),
                Size = new Size(75, 25),
                DialogResult = DialogResult.OK
            };
            _okButton.Click += OkButton_Click;

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(520, yPos + 10),
                Size = new Size(75, 25),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(_serverUrlLabel);
            this.Controls.Add(_serverUrlTextBox);
            this.Controls.Add(_comfyUIPythonLabel);
            this.Controls.Add(_comfyUIPythonTextBox);
            this.Controls.Add(_browsePythonButton);
            this.Controls.Add(_comfyUIInstallLabel);
            this.Controls.Add(_comfyUIInstallTextBox);
            this.Controls.Add(_browseInstallButton);
            this.Controls.Add(_connectionGroupBox);
            this.Controls.Add(_autoStartCheckBox);
            this.Controls.Add(_rememberPathsCheckBox);
            this.Controls.Add(_pathsGroupBox);
            this.Controls.Add(_okButton);
            this.Controls.Add(_cancelButton);

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;

            // Update paths group box enabled state based on checkbox
            _rememberPathsCheckBox.CheckedChanged += (s, e) =>
            {
                _pathsGroupBox.Enabled = _rememberPathsCheckBox.Checked;
            };

            // Update server URL when it changes
            _serverUrlTextBox.TextChanged += (s, e) =>
            {
                UpdateServerManager();
            };

            // Check connection on load
            this.Load += async (s, e) =>
            {
                UpdateServerManager();
                await CheckConnectionAsync();
            };
        }

        private void UpdateServerManager()
        {
            _serverManager?.Dispose();
            _serverManager = new ComfyUIServerManager(
                _serverUrlTextBox.Text.Trim(),
                _comfyUIPythonTextBox.Text.Trim(),
                _comfyUIInstallTextBox.Text.Trim()
            );
        }

        private async void CheckConnectionButton_Click(object? sender, EventArgs e)
        {
            await CheckConnectionAsync();
        }

        private async Task CheckConnectionAsync()
        {
            _checkConnectionButton.Enabled = false;
            _connectionStatusLabel.Text = "Status: Checking...";
            _connectionStatusLabel.ForeColor = Color.Blue;

            try
            {
                UpdateServerManager();
                bool isRunning = await _serverManager!.IsServerRunningAsync();

                if (isRunning)
                {
                    _connectionStatusLabel.Text = "Status: Connected";
                    _connectionStatusLabel.ForeColor = Color.Green;
                }
                else
                {
                    _connectionStatusLabel.Text = "Status: Not connected";
                    _connectionStatusLabel.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                _connectionStatusLabel.Text = $"Status: Error - {ex.Message}";
                _connectionStatusLabel.ForeColor = Color.Red;
            }
            finally
            {
                _checkConnectionButton.Enabled = true;
            }
        }

        private void TerminateButton_Click(object? sender, EventArgs e)
        {
            try
            {
                UpdateServerManager();
                _serverManager!.TerminateAllComfyUIProcesses();
                _connectionStatusLabel.Text = "Status: Terminated";
                _connectionStatusLabel.ForeColor = Color.Orange;
                
                // Check connection after a short delay
                _ = Task.Delay(1000).ContinueWith(async _ =>
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(async () => await CheckConnectionAsync());
                    }
                    else
                    {
                        await CheckConnectionAsync();
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error terminating ComfyUI: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ReconnectButton_Click(object? sender, EventArgs e)
        {
            await CheckConnectionAsync();
        }

        private async void OpenBrowserButton_Click(object? sender, EventArgs e)
        {
            _openBrowserButton.Enabled = false;
            
            try
            {
                UpdateServerManager();
                
                // Check if server is running first
                bool isRunning = await _serverManager!.IsServerRunningAsync();
                
                if (!isRunning)
                {
                    MessageBox.Show(
                        "ComfyUI server is not running. Please start ComfyUI first or enable auto-start in settings.",
                        "ComfyUI Not Running",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    _openBrowserButton.Enabled = true;
                    return;
                }
                
                // Open browser to ComfyUI
                try
                {
                    string serverUrl = _serverUrlTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(serverUrl))
                    {
                        serverUrl = "http://localhost:8188";
                    }
                    
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = serverUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to open browser: {ex.Message}\n\nPlease open {_serverUrlTextBox.Text.Trim()} manually.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                _openBrowserButton.Enabled = true;
            }
        }

        private async void LoadWorkflowButton_Click(object? sender, EventArgs e)
        {
            _loadWorkflowButton.Enabled = false;
            
            try
            {
                // Check if workflow path is set
                string workflowPath = _lastWorkflowTextBox.Text.Trim();
                if (string.IsNullOrEmpty(workflowPath) || !File.Exists(workflowPath))
                {
                    MessageBox.Show(
                        "No workflow file is configured or the file does not exist.\n\nPlease set a workflow path in the Saved Paths section.",
                        "Workflow Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    _loadWorkflowButton.Enabled = true;
                    return;
                }

                UpdateServerManager();
                
                // Check if server is running
                bool isRunning = await _serverManager!.IsServerRunningAsync();
                
                if (!isRunning)
                {
                    MessageBox.Show(
                        "ComfyUI server is not running. Please start ComfyUI first or enable auto-start in settings.",
                        "ComfyUI Not Running",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    _loadWorkflowButton.Enabled = true;
                    return;
                }
                
                // Read workflow file
                string workflowJson = await File.ReadAllTextAsync(workflowPath);
                
                // Load workflow into ComfyUI automatically
                string serverUrl = _serverUrlTextBox.Text.Trim();
                if (string.IsNullOrEmpty(serverUrl))
                {
                    serverUrl = "http://localhost:8188";
                }
                
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(serverUrl);
                    client.Timeout = TimeSpan.FromSeconds(10);
                    
                    try
                    {
                        // Open ComfyUI in browser
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = serverUrl,
                            UseShellExecute = true
                        });
                        
                        // Wait for browser to open
                        await Task.Delay(1000);
                        
                        // Open the workflow file in file explorer/editor so user can easily drag it
                        // This makes it easy to drag and drop into ComfyUI
                        try
                        {
                            // Open file location in explorer and select the file
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = $"/select,\"{workflowPath}\"",
                                UseShellExecute = true
                            });
                        }
                        catch
                        {
                            // If explorer fails, try opening the file directly
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = workflowPath,
                                    UseShellExecute = true
                                });
                            }
                            catch { }
                        }
                        
                        // Copy path to clipboard as backup
                        try
                        {
                            Clipboard.SetText(workflowPath);
                        }
                        catch { }
                        
                        // Show brief message
                        MessageBox.Show(
                            $"ComfyUI opened and workflow file location shown.\n\n" +
                            $"Quick load options:\n" +
                            $"1. Drag the workflow file from Explorer into ComfyUI (easiest!)\n" +
                            $"2. In ComfyUI, press Ctrl+O and select the file\n" +
                            $"3. Press Ctrl+V to paste the path (copied to clipboard)\n\n" +
                            $"File: {Path.GetFileName(workflowPath)}",
                            "Workflow Ready",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Failed to load workflow: {ex.Message}",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading workflow: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                _loadWorkflowButton.Enabled = true;
            }
        }

        private void LoadSettings()
        {
            _serverUrlTextBox.Text = Settings.ServerUrl;
            _comfyUIPythonTextBox.Text = Settings.ComfyUIPythonPath;
            _comfyUIInstallTextBox.Text = Settings.ComfyUIInstallPath;
            _autoStartCheckBox.Checked = Settings.AutoStartComfyUI;
            _rememberPathsCheckBox.Checked = Settings.RememberPaths;
            _lastWorkflowTextBox.Text = Settings.LastWorkflowPath;
            _lastOutputTextBox.Text = Settings.LastOutputDirectory;
            _pathsGroupBox.Enabled = Settings.RememberPaths;
        }

        private void BrowseWorkflowButton_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                dialog.Title = "Select ComfyUI Workflow";
                
                if (!string.IsNullOrEmpty(_lastWorkflowTextBox.Text) && File.Exists(_lastWorkflowTextBox.Text))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(_lastWorkflowTextBox.Text);
                    dialog.FileName = Path.GetFileName(_lastWorkflowTextBox.Text);
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _lastWorkflowTextBox.Text = dialog.FileName;
                }
            }
        }

        private async void LoadWorkflowFromPathButton_Click(object? sender, EventArgs e)
        {
            _browseWorkflowButton.Enabled = false;
            
            try
            {
                // Get workflow path from text box
                string workflowPath = _lastWorkflowTextBox.Text.Trim();
                
                // If no path or file doesn't exist, prompt to browse first
                if (string.IsNullOrEmpty(workflowPath) || !File.Exists(workflowPath))
                {
                    DialogResult result = MessageBox.Show(
                        "No workflow file is set or the file doesn't exist.\n\nWould you like to browse for a workflow file?",
                        "Workflow Not Found",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );
                    
                    if (result == DialogResult.Yes)
                    {
                        using (OpenFileDialog dialog = new OpenFileDialog())
                        {
                            dialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                            dialog.Title = "Select ComfyUI Workflow";
                            
                            if (!string.IsNullOrEmpty(_lastWorkflowTextBox.Text) && File.Exists(_lastWorkflowTextBox.Text))
                            {
                                dialog.InitialDirectory = Path.GetDirectoryName(_lastWorkflowTextBox.Text);
                                dialog.FileName = Path.GetFileName(_lastWorkflowTextBox.Text);
                            }

                            if (dialog.ShowDialog() == DialogResult.OK)
                            {
                                _lastWorkflowTextBox.Text = dialog.FileName;
                                workflowPath = dialog.FileName;
                            }
                            else
                            {
                                _browseWorkflowButton.Enabled = true;
                                return;
                            }
                        }
                    }
                    else
                    {
                        _browseWorkflowButton.Enabled = true;
                        return;
                    }
                }

                UpdateServerManager();
                
                // Check if server is running
                bool isRunning = await _serverManager!.IsServerRunningAsync();
                
                if (!isRunning)
                {
                    DialogResult result = MessageBox.Show(
                        "ComfyUI server is not running.\n\nWould you like to start it now?",
                        "ComfyUI Not Running",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );
                    
                    if (result == DialogResult.Yes)
                    {
                        var progress = new Progress<string>(status => { });
                        bool started = await _serverManager.EnsureServerRunningAsync(progress);
                        
                        if (!started)
                        {
                            MessageBox.Show(
                                "Failed to start ComfyUI server. Please check your settings.",
                                "Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error
                            );
                            _browseWorkflowButton.Enabled = true;
                            return;
                        }
                    }
                    else
                    {
                        _browseWorkflowButton.Enabled = true;
                        return;
                    }
                }
                
                // Load the workflow
                string serverUrl = _serverUrlTextBox.Text.Trim();
                if (string.IsNullOrEmpty(serverUrl))
                {
                    serverUrl = "http://localhost:8188";
                }
                
                // Open ComfyUI in browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = serverUrl,
                    UseShellExecute = true
                });
                
                // Wait for browser to open
                await Task.Delay(1000);
                
                // Open the workflow file location in explorer
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{workflowPath}\"",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = workflowPath,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
                
                // Copy path to clipboard
                try
                {
                    Clipboard.SetText(workflowPath);
                }
                catch { }
                
                // Show brief message
                MessageBox.Show(
                    $"ComfyUI opened and workflow file location shown.\n\n" +
                    $"Quick load options:\n" +
                    $"1. Drag the workflow file from Explorer into ComfyUI (easiest!)\n" +
                    $"2. In ComfyUI, press Ctrl+O and select the file\n" +
                    $"3. Press Ctrl+V to paste the path (copied to clipboard)\n\n" +
                    $"File: {Path.GetFileName(workflowPath)}",
                    "Workflow Ready",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading workflow: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                _browseWorkflowButton.Enabled = true;
            }
        }

        private void BrowseOutputButton_Click(object? sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select output directory for generated tiles";
                dialog.ShowNewFolderButton = true;
                
                if (!string.IsNullOrEmpty(_lastOutputTextBox.Text) && Directory.Exists(_lastOutputTextBox.Text))
                {
                    dialog.SelectedPath = _lastOutputTextBox.Text;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _lastOutputTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void BrowsePythonButton_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Python executable (python.exe)|python.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*";
                dialog.Title = "Select Python Executable";
                
                if (!string.IsNullOrEmpty(_comfyUIPythonTextBox.Text) && File.Exists(_comfyUIPythonTextBox.Text))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(_comfyUIPythonTextBox.Text);
                    dialog.FileName = Path.GetFileName(_comfyUIPythonTextBox.Text);
                }
                else
                {
                    // Common Python locations
                    string[] commonPaths = {
                        @"C:\Python39\python.exe",
                        @"C:\Python310\python.exe",
                        @"C:\Python311\python.exe",
                        @"C:\Python312\python.exe",
                        @"C:\Program Files\Python39\python.exe",
                        @"C:\Program Files\Python310\python.exe",
                        @"C:\Program Files\Python311\python.exe",
                        @"C:\Program Files\Python312\python.exe"
                    };
                    
                    foreach (string path in commonPaths)
                    {
                        if (File.Exists(path))
                        {
                            dialog.InitialDirectory = Path.GetDirectoryName(path);
                            dialog.FileName = Path.GetFileName(path);
                            break;
                        }
                    }
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _comfyUIPythonTextBox.Text = dialog.FileName;
                }
            }
        }

        private void BrowseInstallButton_Click(object? sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select ComfyUI installation directory (should contain main.py)";
                dialog.ShowNewFolderButton = false;
                
                if (!string.IsNullOrEmpty(_comfyUIInstallTextBox.Text) && Directory.Exists(_comfyUIInstallTextBox.Text))
                {
                    dialog.SelectedPath = _comfyUIInstallTextBox.Text;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _comfyUIInstallTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            Settings.ServerUrl = _serverUrlTextBox.Text.Trim();
            Settings.ComfyUIPythonPath = _comfyUIPythonTextBox.Text.Trim();
            Settings.ComfyUIInstallPath = _comfyUIInstallTextBox.Text.Trim();
            Settings.AutoStartComfyUI = _autoStartCheckBox.Checked;
            Settings.RememberPaths = _rememberPathsCheckBox.Checked;
            
            if (Settings.RememberPaths)
            {
                Settings.LastWorkflowPath = _lastWorkflowTextBox.Text;
                Settings.LastOutputDirectory = _lastOutputTextBox.Text;
            }
            else
            {
                Settings.LastWorkflowPath = "";
                Settings.LastOutputDirectory = "";
            }

            Settings.Save();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _serverManager?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

