using System;
using System.IO;
using System.Text.Json;

namespace Project9.Editor
{
    public class ComfyUISettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Project9",
            "comfyui_settings.json"
        );

        public string ServerUrl { get; set; } = "http://localhost:8188";
        public string ComfyUIPythonPath { get; set; } = "";
        public string ComfyUIInstallPath { get; set; } = "";
        public bool AutoStartComfyUI { get; set; } = false;
        public string LastWorkflowPath { get; set; } = "";
        public string LastOutputDirectory { get; set; } = "";
        public bool RememberPaths { get; set; } = true;

        public static ComfyUISettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<ComfyUISettings>(json);
                    return settings ?? new ComfyUISettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading ComfyUI settings: {ex.Message}");
            }

            return new ComfyUISettings();
        }

        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath) ?? "";
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving ComfyUI settings: {ex.Message}");
            }
        }
    }
}


