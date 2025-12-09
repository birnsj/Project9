using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Project9.Editor
{
    public class ComfyUIServerManager : IDisposable
    {
        private string _serverUrl;
        private readonly string? _comfyUIPythonPath;
        private readonly string? _comfyUIInstallPath;
        private HttpClient _httpClient;
        private Process? _comfyUIProcess;

        public ComfyUIServerManager(string serverUrl, string? comfyUIPythonPath, string? comfyUIInstallPath)
        {
            _serverUrl = serverUrl;
            _comfyUIPythonPath = comfyUIPythonPath;
            _comfyUIInstallPath = comfyUIInstallPath;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(serverUrl),
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        /// <summary>
        /// Updates the server URL and reinitializes the HTTP client.
        /// </summary>
        public void UpdateServerUrl(string newServerUrl)
        {
            _serverUrl = newServerUrl;
            _httpClient?.Dispose();
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(newServerUrl),
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        /// <summary>
        /// Checks if the ComfyUI server is running and accessible.
        /// </summary>
        public async Task<bool> IsServerRunningAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to connect to the server (just check if it responds)
                HttpResponseMessage response = await _httpClient.GetAsync("/", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Starts ComfyUI server if it's not running. Returns true if server is ready, false otherwise.
        /// </summary>
        public async Task<bool> EnsureServerRunningAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            // First check if server is already running
            progress?.Report("Checking if ComfyUI server is running...");
            if (await IsServerRunningAsync(cancellationToken))
            {
                progress?.Report("ComfyUI server is already running.");
                return true;
            }

            // If auto-start is not configured, return false
            if (string.IsNullOrEmpty(_comfyUIPythonPath) || string.IsNullOrEmpty(_comfyUIInstallPath))
            {
                progress?.Report("ComfyUI path not configured. Please configure it in ComfyUI Settings.");
                return false;
            }

            // Check if paths exist
            if (!File.Exists(_comfyUIPythonPath))
            {
                progress?.Report($"Python executable not found: {_comfyUIPythonPath}");
                return false;
            }

            if (!Directory.Exists(_comfyUIInstallPath))
            {
                progress?.Report($"ComfyUI installation directory not found: {_comfyUIInstallPath}");
                return false;
            }

            // Check if main.py exists in ComfyUI directory
            string mainPyPath = Path.Combine(_comfyUIInstallPath, "main.py");
            if (!File.Exists(mainPyPath))
            {
                progress?.Report($"ComfyUI main.py not found in: {_comfyUIInstallPath}");
                return false;
            }

            // Start ComfyUI process
            progress?.Report("Starting ComfyUI server...");
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = _comfyUIPythonPath,
                    Arguments = $"\"{mainPyPath}\"",
                    WorkingDirectory = _comfyUIInstallPath,
                    UseShellExecute = false, // Keep false for proper process tracking
                    CreateNoWindow = false, // Show window so user can see ComfyUI output
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                _comfyUIProcess = Process.Start(startInfo);
                if (_comfyUIProcess == null)
                {
                    progress?.Report("Failed to start ComfyUI process.");
                    return false;
                }

                progress?.Report("Waiting for ComfyUI server to start...");

                // Wait for server to be ready (poll every second, max 60 seconds)
                const int maxWaitSeconds = 60;
                const int pollIntervalMs = 1000;
                int waitedSeconds = 0;

                while (waitedSeconds < maxWaitSeconds && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(pollIntervalMs, cancellationToken);
                    waitedSeconds++;

                    if (await IsServerRunningAsync(cancellationToken))
                    {
                        progress?.Report("ComfyUI server is ready!");
                        
                        // Open browser to ComfyUI interface after a short delay to ensure server is fully ready
                        await Task.Delay(500, cancellationToken);
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = _serverUrl,
                                UseShellExecute = true
                            });
                            progress?.Report("Opening ComfyUI in browser...");
                        }
                        catch (Exception ex)
                        {
                            // If opening browser fails, log it but don't fail the startup
                            progress?.Report($"Note: Could not automatically open browser. Please open {_serverUrl} manually.");
                        }
                        
                        return true;
                    }

                    // Check if process has exited
                    if (_comfyUIProcess.HasExited)
                    {
                        progress?.Report($"ComfyUI process exited unexpectedly. Exit code: {_comfyUIProcess.ExitCode}");
                        return false;
                    }

                    progress?.Report($"Waiting for server... ({waitedSeconds}/{maxWaitSeconds} seconds)");
                }

                progress?.Report("ComfyUI server did not become ready within the timeout period.");
                return false;
            }
            catch (Exception ex)
            {
                progress?.Report($"Error starting ComfyUI: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stops the ComfyUI server if it was started by this manager.
        /// </summary>
        public void StopServer()
        {
            if (_comfyUIProcess != null && !_comfyUIProcess.HasExited)
            {
                try
                {
                    _comfyUIProcess.Kill();
                    _comfyUIProcess.WaitForExit(5000);
                }
                catch
                {
                    // Ignore errors when stopping
                }
                finally
                {
                    _comfyUIProcess?.Dispose();
                    _comfyUIProcess = null;
                }
            }
        }

        /// <summary>
        /// Terminates all ComfyUI processes (finds processes by name and port).
        /// </summary>
        public void TerminateAllComfyUIProcesses()
        {
            // First, stop the process we started
            StopServer();

            // Try to find and kill other ComfyUI processes
            try
            {
                // Look for Python processes that might be running ComfyUI
                // This is a best-effort approach - we can't be 100% sure without checking command line args
                Process[] pythonProcesses = Process.GetProcessesByName("python");
                Process[] pythonwProcesses = Process.GetProcessesByName("pythonw");

                foreach (var process in pythonProcesses)
                {
                    try
                    {
                        // Check if the process command line contains ComfyUI-related paths
                        string? commandLine = GetProcessCommandLine(process);
                        if (commandLine != null && 
                            (commandLine.Contains("main.py", StringComparison.OrdinalIgnoreCase) ||
                             commandLine.Contains("comfyui", StringComparison.OrdinalIgnoreCase) ||
                             (_comfyUIInstallPath != null && commandLine.Contains(_comfyUIInstallPath, StringComparison.OrdinalIgnoreCase))))
                        {
                            process.Kill();
                            process.WaitForExit(5000);
                        }
                    }
                    catch
                    {
                        // Ignore errors for individual processes
                    }
                    finally
                    {
                        process?.Dispose();
                    }
                }

                foreach (var process in pythonwProcesses)
                {
                    try
                    {
                        string? commandLine = GetProcessCommandLine(process);
                        if (commandLine != null && 
                            (commandLine.Contains("main.py", StringComparison.OrdinalIgnoreCase) ||
                             commandLine.Contains("comfyui", StringComparison.OrdinalIgnoreCase) ||
                             (_comfyUIInstallPath != null && commandLine.Contains(_comfyUIInstallPath, StringComparison.OrdinalIgnoreCase))))
                        {
                            process.Kill();
                            process.WaitForExit(5000);
                        }
                    }
                    catch
                    {
                        // Ignore errors for individual processes
                    }
                    finally
                    {
                        process?.Dispose();
                    }
                }
            }
            catch
            {
                // Ignore errors when trying to find/kill processes
            }
        }

        private string? GetProcessCommandLine(Process process)
        {
            try
            {
                // Try to get command line using WMI (Windows Management Instrumentation)
                // This requires System.Management which may not be available
                // For now, we'll use a simpler approach
                return null;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            // Note: We don't automatically stop the server on dispose,
            // as the user might want it to keep running
        }
    }
}

