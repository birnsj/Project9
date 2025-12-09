using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Project9.Editor
{
    public class ComfyUIWorkflowExecutor : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private const string DefaultBaseUrl = "http://localhost:8188";

        public ComfyUIWorkflowExecutor(string? baseUrl = null)
        {
            _baseUrl = baseUrl ?? DefaultBaseUrl;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromMinutes(30) // Workflows can take a while
            };
        }

        /// <summary>
        /// Checks if the ComfyUI server is accessible and responding.
        /// </summary>
        public async Task<bool> IsServerAccessibleAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using (HttpClient testClient = new HttpClient())
                {
                    testClient.BaseAddress = new Uri(_baseUrl);
                    testClient.Timeout = TimeSpan.FromSeconds(5);
                    HttpResponseMessage response = await testClient.GetAsync("/", cancellationToken);
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<ExecutionResult> ExecuteWorkflowAsync(
            string workflowJsonPath,
            string outputDirectory,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // First, verify server is accessible
                progress?.Report("Checking ComfyUI server connection...");
                bool serverAccessible = await IsServerAccessibleAsync(cancellationToken);
                if (!serverAccessible)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        ErrorMessage = $"Cannot connect to ComfyUI server at {_baseUrl}.\n\n" +
                                       "Please ensure:\n" +
                                       "1. ComfyUI is running\n" +
                                       "2. The server URL is correct\n" +
                                       "3. ComfyUI is accessible at the configured address"
                    };
                }
                
                progress?.Report("Server connection verified. Reading workflow file...");
                
                // Read and parse workflow JSON
                string workflowJson = await File.ReadAllTextAsync(workflowJsonPath, cancellationToken);
                JsonDocument workflowDoc;
                try
                {
                    workflowDoc = JsonDocument.Parse(workflowJson);
                }
                catch (JsonException ex)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        ErrorMessage = $"Failed to parse workflow JSON file.\n\nError: {ex.Message}\n\nPlease ensure the workflow file is valid JSON."
                    };
                }
                
                // Validate workflow structure
                JsonElement root = workflowDoc.RootElement;
                if (root.ValueKind != JsonValueKind.Object && root.ValueKind != JsonValueKind.Array)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        ErrorMessage = $"Invalid workflow structure. Expected Object or Array, got {root.ValueKind}.\n\nPlease ensure the workflow file is a valid ComfyUI workflow."
                    };
                }
                
                // Modify workflow to set output directory
                progress?.Report($"Configuring output directory: {outputDirectory}");
                JsonElement modifiedWorkflow;
                try
                {
                    modifiedWorkflow = ModifyWorkflowOutputDirectory(workflowDoc, outputDirectory);
                    
                    // Debug: Check what we got back
                    if (modifiedWorkflow.ValueKind == JsonValueKind.Undefined)
                    {
                        throw new Exception("Modified workflow is undefined. The workflow structure may not be recognized.");
                    }
                    
                    progress?.Report($"Output directory configured. Files will be saved to: {outputDirectory}");
                }
                catch (Exception ex)
                {
                    // If modification fails, log warning but continue with original workflow
                    progress?.Report($"Warning: Could not modify output directory. Using original workflow. Error: {ex.Message}");
                    progress?.Report($"Note: Files may be saved to ComfyUI's default output directory instead of: {outputDirectory}");
                    
                    // Try to extract the actual workflow from the root
                    JsonElement rootElement = workflowDoc.RootElement;
                    if (rootElement.ValueKind == JsonValueKind.Object)
                    {
                        // Check if it has a "prompt" or "workflow" property
                        if (rootElement.TryGetProperty("prompt", out JsonElement prompt))
                        {
                            modifiedWorkflow = prompt;
                        }
                        else if (rootElement.TryGetProperty("workflow", out JsonElement workflow))
                        {
                            modifiedWorkflow = workflow;
                        }
                        else
                        {
                            // Use root as-is
                            modifiedWorkflow = rootElement;
                        }
                    }
                    else
                    {
                        modifiedWorkflow = rootElement;
                    }
                }

                // Submit workflow (this queues and runs it automatically)
                progress?.Report("Submitting workflow to ComfyUI...");
                string promptId = await SubmitWorkflowAsync(modifiedWorkflow, cancellationToken);
                
                if (string.IsNullOrEmpty(promptId))
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to submit workflow. No prompt ID returned."
                    };
                }

                // Poll for completion
                progress?.Report($"Workflow queued and running (ID: {promptId}). Waiting for completion...");
                
                // Verify workflow is actually in the queue
                await Task.Delay(500, cancellationToken); // Brief delay to ensure it's queued
                progress?.Report($"Workflow is executing in ComfyUI. You can see it in the ComfyUI interface.");
                try
                {
                    bool completed = await PollForCompletionAsync(promptId, progress, cancellationToken);
                    
                    if (!completed)
                    {
                        return new ExecutionResult
                        {
                            Success = false,
                            ErrorMessage = "Workflow execution was cancelled or timed out."
                        };
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("Workflow error") || ex.Message.Contains("Workflow execution failed"))
                {
                    // Re-throw workflow errors so they're caught by the outer handler
                    throw;
                }

                progress?.Report("Workflow completed successfully!");
                
                return new ExecutionResult
                {
                    Success = true,
                    PromptId = promptId
                };
            }
            catch (HttpRequestException ex)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Network error connecting to ComfyUI server at {_baseUrl}.\n\n" +
                                   $"Error: {ex.Message}\n\n" +
                                   "Please ensure ComfyUI is running and the server URL is correct."
                };
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Request to ComfyUI server timed out.\n\n" +
                                   "The server may be busy or not responding. Please try again."
                };
            }
            catch (JsonException ex)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Invalid JSON response from ComfyUI server.\n\n" +
                                   $"Error: {ex.Message}\n\n" +
                                   "The workflow file may be malformed or the server returned an unexpected response."
                };
            }
            catch (Exception ex)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Error executing workflow: {ex.Message}\n\n" +
                                   $"Type: {ex.GetType().Name}\n" +
                                   $"Stack trace: {ex.StackTrace}"
                };
            }
        }

        private JsonElement ModifyWorkflowOutputDirectory(JsonDocument workflowDoc, string outputDirectory)
        {
            try
            {
                // Ensure output directory exists
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                // Convert to absolute path and normalize separators for ComfyUI (uses forward slashes)
                string absoluteOutputPath = Path.GetFullPath(outputDirectory).Replace('\\', '/');
                if (!absoluteOutputPath.EndsWith("/"))
                {
                    absoluteOutputPath += "/";
                }
                
                // Find all SaveImage nodes and modify their output path
                JsonElement root = workflowDoc.RootElement;
                JsonElement workflowElement = root;
                
                // ComfyUI workflows can be structured in different ways
                // Try to find the actual workflow data
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("workflow", out JsonElement workflow))
                    {
                        workflowElement = workflow;
                    }
                    else if (root.TryGetProperty("prompt", out JsonElement prompt))
                    {
                        workflowElement = prompt;
                    }
                }

                // Helper function to convert JsonElement to proper .NET type
                object ConvertJsonElement(JsonElement element)
                {
                    try
                    {
                        return element.ValueKind switch
                        {
                            JsonValueKind.String => element.GetString() ?? "",
                            JsonValueKind.Number => element.TryGetInt32(out int intVal) ? intVal : (object)element.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => (object?)null,
                            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
                            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                                prop => prop.Name,
                                prop => ConvertJsonElement(prop.Value)
                            ),
                            _ => element.GetRawText()
                        };
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error converting JSON element (ValueKind: {element.ValueKind}): {ex.Message}", ex);
                    }
                }

                // ComfyUI workflows are structured as objects with node IDs as keys
                if (workflowElement.ValueKind == JsonValueKind.Object)
                {
                    // Build modified workflow as dictionary for easier manipulation
                    Dictionary<string, object> modifiedWorkflow = new Dictionary<string, object>();
                    
                    foreach (JsonProperty property in workflowElement.EnumerateObject())
                    {
                        try
                        {
                            if (property.Value.ValueKind == JsonValueKind.Object)
                            {
                                // Parse node to check if it's SaveImage
                                Dictionary<string, object> node = new Dictionary<string, object>();
                                bool isSaveImage = false;
                                Dictionary<string, object>? inputs = null;
                                
                                foreach (JsonProperty nodeProp in property.Value.EnumerateObject())
                                {
                                    try
                                    {
                                        if (nodeProp.Name == "class_type")
                                        {
                                            if (nodeProp.Value.ValueKind == JsonValueKind.String)
                                            {
                                                string? classType = nodeProp.Value.GetString();
                                                isSaveImage = classType == "SaveImage";
                                                node[nodeProp.Name] = classType ?? "";
                                            }
                                            else
                                            {
                                                node[nodeProp.Name] = ConvertJsonElement(nodeProp.Value);
                                            }
                                        }
                                        else if (nodeProp.Name == "inputs" && nodeProp.Value.ValueKind == JsonValueKind.Object)
                                        {
                                            inputs = new Dictionary<string, object>();
                                            foreach (JsonProperty inputProp in nodeProp.Value.EnumerateObject())
                                            {
                                                try
                                                {
                                                    if (isSaveImage && inputProp.Name == "filename_prefix")
                                                    {
                                                        // Modify filename_prefix to include output directory
                                                        // Check if it's actually a string before trying to get it
                                                        if (inputProp.Value.ValueKind == JsonValueKind.String)
                                                        {
                                                            string prefix = inputProp.Value.GetString() ?? "";
                                                            // ComfyUI SaveImage: filename_prefix can include path
                                                            // Use absolute path to ensure files are saved to the correct location
                                                            inputs[inputProp.Name] = absoluteOutputPath + prefix;
                                                        }
                                                        else
                                                        {
                                                            // If filename_prefix is not a string, convert it and then modify
                                                            // This handles cases where it might be an object or array
                                                            object prefixValue = ConvertJsonElement(inputProp.Value);
                                                            if (prefixValue is string prefixStr)
                                                            {
                                                                inputs[inputProp.Name] = absoluteOutputPath + prefixStr;
                                                            }
                                                            else
                                                            {
                                                                // If it's not a string, just prepend the path somehow
                                                                inputs[inputProp.Name] = absoluteOutputPath + prefixValue.ToString();
                                                            }
                                                        }
                                                    }
                                                    else if (isSaveImage && inputProp.Name == "output_path")
                                                    {
                                                        // Some ComfyUI workflows use output_path instead of filename_prefix
                                                        inputs[inputProp.Name] = absoluteOutputPath;
                                                    }
                                                    else
                                                    {
                                                        // Copy other input properties using proper type conversion
                                                        inputs[inputProp.Name] = ConvertJsonElement(inputProp.Value);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    throw new Exception($"Error processing input property '{inputProp.Name}' (ValueKind: {inputProp.Value.ValueKind}): {ex.Message}", ex);
                                                }
                                            }
                                            node[nodeProp.Name] = inputs;
                                        }
                                        else
                                        {
                                            // Copy other properties using proper type conversion
                                            node[nodeProp.Name] = ConvertJsonElement(nodeProp.Value);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        throw new Exception($"Error processing node property '{nodeProp.Name}' in node '{property.Name}': {ex.Message}", ex);
                                    }
                                }
                                
                                modifiedWorkflow[property.Name] = node;
                            }
                            else
                            {
                                modifiedWorkflow[property.Name] = ConvertJsonElement(property.Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Error processing workflow property '{property.Name}': {ex.Message}", ex);
                        }
                    }
                    
                    // Convert back to JsonElement
                    string modifiedJson = JsonSerializer.Serialize(modifiedWorkflow);
                    JsonDocument modifiedDoc = JsonDocument.Parse(modifiedJson);
                    return modifiedDoc.RootElement;
                }
                
                // If modification failed, return original
                return workflowElement;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error modifying workflow output directory: {ex.Message}\n\nInner exception: {ex.InnerException?.Message}", ex);
            }
        }

        private object? ConvertJsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "",
                JsonValueKind.Number => element.TryGetInt32(out int intVal) ? intVal : (object)element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => (object?)null,
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToObject).ToArray(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                    prop => prop.Name,
                    prop => ConvertJsonElementToObject(prop.Value)
                ),
                _ => element.GetRawText()
            };
        }

        private async Task<string> SubmitWorkflowAsync(JsonElement workflow, CancellationToken cancellationToken)
        {
            try
            {
                // ComfyUI expects the workflow in a specific format: { "prompt": { ... } }
                // Use JsonSerializer with JsonElement support
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    WriteIndented = false
                };
                
                // Build request body using JsonDocument to ensure proper structure
                using (MemoryStream stream = new MemoryStream())
                {
                    using (Utf8JsonWriter writer = new Utf8JsonWriter(stream))
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName("prompt");
                        workflow.WriteTo(writer);
                        writer.WriteEndObject();
                    }
                    
                    stream.Position = 0;
                    string requestJson = Encoding.UTF8.GetString(stream.ToArray());
                    
                    // Validate the JSON is valid
                    try
                    {
                        JsonDocument.Parse(requestJson);
                    }
                    catch (JsonException ex)
                    {
                        throw new Exception($"Invalid JSON structure after wrapping: {ex.Message}", ex);
                    }
                    
                    StringContent content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await _httpClient.PostAsync("/prompt", content, cancellationToken);
                    
                    string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        // Try to parse error message from ComfyUI
                        string errorDetails = responseJson;
                        try
                        {
                            JsonDocument errorDoc = JsonDocument.Parse(responseJson);
                            if (errorDoc.RootElement.TryGetProperty("error", out JsonElement errorElement))
                            {
                                if (errorElement.ValueKind == JsonValueKind.Object)
                                {
                                    // Try to get detailed error info
                                    if (errorElement.TryGetProperty("message", out JsonElement msg))
                                    {
                                        errorDetails = msg.GetString() ?? errorDetails;
                                    }
                                    else if (errorElement.TryGetProperty("type", out JsonElement type) && errorElement.TryGetProperty("message", out JsonElement msg2))
                                    {
                                        errorDetails = $"{type.GetString()}: {msg2.GetString()}";
                                    }
                                    else
                                    {
                                        errorDetails = errorElement.GetRawText();
                                    }
                                }
                                else
                                {
                                    errorDetails = errorElement.GetString() ?? errorDetails;
                                }
                            }
                            else if (errorDoc.RootElement.TryGetProperty("message", out JsonElement messageElement))
                            {
                                errorDetails = messageElement.GetString() ?? errorDetails;
                            }
                            else if (errorDoc.RootElement.TryGetProperty("errors", out JsonElement errors))
                            {
                                // ComfyUI sometimes returns errors as an array
                                if (errors.ValueKind == JsonValueKind.Array)
                                {
                                    var errorList = new List<string>();
                                    foreach (var err in errors.EnumerateArray())
                                    {
                                        if (err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out JsonElement errMsg))
                                        {
                                            errorList.Add(errMsg.GetString() ?? "Unknown error");
                                        }
                                        else
                                        {
                                            errorList.Add(err.GetString() ?? err.GetRawText());
                                        }
                                    }
                                    errorDetails = string.Join("\n", errorList);
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // If we can't parse JSON, just use the raw response
                        }
                        
                        throw new Exception($"Failed to submit workflow to ComfyUI.\n\nHTTP {response.StatusCode}\n\nError details:\n{errorDetails}\n\nFull response:\n{responseJson}");
                    }

                    JsonDocument responseDoc = JsonDocument.Parse(responseJson);
                    
                    // Check for errors in the response
                    if (responseDoc.RootElement.TryGetProperty("error", out JsonElement errorElem))
                    {
                        string errorMessage = errorElem.GetString() ?? "Unknown error";
                        throw new Exception($"ComfyUI returned an error: {errorMessage}");
                    }
                    
                    if (responseDoc.RootElement.TryGetProperty("prompt_id", out JsonElement promptIdElement))
                    {
                        return promptIdElement.GetString() ?? "";
                    }

                    throw new Exception("ComfyUI did not return a prompt_id in the response.");
                }
            }
            catch (Exception ex) when (!(ex is HttpRequestException || ex is TaskCanceledException))
            {
                // Re-throw with more context if it's a serialization or other error
                string innerMessage = ex.InnerException != null ? $"\n\nInner exception: {ex.InnerException.Message}" : "";
                string stackTrace = ex.StackTrace != null ? $"\n\nStack trace: {ex.StackTrace}" : "";
                throw new Exception($"Error preparing workflow for submission: {ex.Message}{innerMessage}{stackTrace}", ex);
            }
        }

        private async Task<bool> PollForCompletionAsync(
            string promptId,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            const int pollIntervalMs = 1000; // Poll every second
            const int maxWaitTimeMs = 300000; // Max 5 minutes
            
            DateTime startTime = DateTime.Now;
            string? lastError = null;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > maxWaitTimeMs)
                {
                    progress?.Report("Workflow execution timed out.");
                    if (!string.IsNullOrEmpty(lastError))
                    {
                        throw new Exception($"Workflow execution timed out. Last error: {lastError}");
                    }
                    return false;
                }

                try
                {
                    // Check queue status with retry logic for connection issues
                    HttpResponseMessage queueResponse = null;
                    int retryCount = 0;
                    const int maxRetries = 3;
                    
                    while (retryCount < maxRetries)
                    {
                        try
                        {
                            queueResponse = await _httpClient.GetAsync("/queue", cancellationToken);
                            if (queueResponse.IsSuccessStatusCode)
                            {
                                break;
                            }
                        }
                        catch (HttpRequestException) when (retryCount < maxRetries - 1)
                        {
                            retryCount++;
                            progress?.Report($"Connection issue, retrying... ({retryCount}/{maxRetries})");
                            await Task.Delay(1000 * retryCount, cancellationToken); // Exponential backoff
                            continue;
                        }
                        catch (TaskCanceledException) when (retryCount < maxRetries - 1)
                        {
                            retryCount++;
                            progress?.Report($"Request timeout, retrying... ({retryCount}/{maxRetries})");
                            await Task.Delay(1000 * retryCount, cancellationToken);
                            continue;
                        }
                        
                        retryCount++;
                    }
                    
                    if (queueResponse != null && queueResponse.IsSuccessStatusCode)
                    {
                        string queueJson = await queueResponse.Content.ReadAsStringAsync(cancellationToken);
                        JsonDocument queueDoc = JsonDocument.Parse(queueJson);
                        
                        // Check for failed items first
                        if (queueDoc.RootElement.TryGetProperty("queue_failed", out JsonElement failed))
                        {
                            foreach (JsonElement item in failed.EnumerateArray())
                            {
                                // Queue items are arrays: [prompt_id, error_info]
                                if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 0)
                                {
                                    string? itemPromptId = item[0].GetString();
                                    if (itemPromptId == promptId)
                                    {
                                        string errorMsg = "Workflow execution failed.";
                                        if (item.GetArrayLength() > 1)
                                        {
                                            if (item[1].ValueKind == JsonValueKind.Object)
                                            {
                                                // Try to extract detailed error information
                                                var errorObj = item[1];
                                                if (errorObj.TryGetProperty("error", out JsonElement errorDetail))
                                                {
                                                    if (errorDetail.ValueKind == JsonValueKind.Object)
                                                    {
                                                        if (errorDetail.TryGetProperty("message", out JsonElement msg))
                                                        {
                                                            errorMsg = $"Workflow error: {msg.GetString() ?? "Unknown error"}";
                                                        }
                                                        else if (errorDetail.TryGetProperty("type", out JsonElement type))
                                                        {
                                                            errorMsg = $"Workflow error ({type.GetString()}): {errorDetail.GetRawText()}";
                                                        }
                                                        else
                                                        {
                                                            errorMsg = $"Workflow error: {errorDetail.GetRawText()}";
                                                        }
                                                    }
                                                    else
                                                    {
                                                        errorMsg = $"Workflow error: {errorDetail.GetString() ?? "Unknown error"}";
                                                    }
                                                }
                                                else if (errorObj.TryGetProperty("message", out JsonElement messageDetail))
                                                {
                                                    errorMsg = $"Workflow error: {messageDetail.GetString() ?? "Unknown error"}";
                                                }
                                                else if (errorObj.TryGetProperty("node_errors", out JsonElement nodeErrors))
                                                {
                                                    // ComfyUI sometimes returns node-specific errors
                                                    errorMsg = $"Workflow error: Node errors detected. Check ComfyUI console for details.\n{nodeErrors.GetRawText()}";
                                                }
                                                else
                                                {
                                                    errorMsg = $"Workflow error: {errorObj.GetRawText()}";
                                                }
                                            }
                                            else if (item[1].ValueKind == JsonValueKind.String)
                                            {
                                                errorMsg = $"Workflow error: {item[1].GetString()}";
                                            }
                                        }
                                        progress?.Report(errorMsg);
                                        lastError = errorMsg;
                                        throw new Exception(errorMsg);
                                    }
                                }
                            }
                        }
                        
                        // Check if our prompt is still in the queue
                        bool inQueue = false;
                        if (queueDoc.RootElement.TryGetProperty("queue_running", out JsonElement running))
                        {
                            foreach (JsonElement item in running.EnumerateArray())
                            {
                                // Queue items are arrays: [prompt_id, ...]
                                if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 0)
                                {
                                    string? itemPromptId = item[0].GetString();
                                    if (itemPromptId == promptId)
                                    {
                                        inQueue = true;
                                        break;
                                    }
                                }
                                // Fallback: try object format
                                else if (item.TryGetProperty("1", out JsonElement promptIdElem))
                                {
                                    if (promptIdElem.GetString() == promptId)
                                    {
                                        inQueue = true;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (!inQueue && queueDoc.RootElement.TryGetProperty("queue_pending", out JsonElement pending))
                        {
                            foreach (JsonElement item in pending.EnumerateArray())
                            {
                                // Queue items are arrays: [prompt_id, ...]
                                if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 0)
                                {
                                    string? itemPromptId = item[0].GetString();
                                    if (itemPromptId == promptId)
                                    {
                                        inQueue = true;
                                        break;
                                    }
                                }
                                // Fallback: try object format
                                else if (item.TryGetProperty("1", out JsonElement promptIdElem))
                                {
                                    if (promptIdElem.GetString() == promptId)
                                    {
                                        inQueue = true;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (!inQueue)
                        {
                            // Check history to confirm completion
                            HttpResponseMessage historyResponse = await _httpClient.GetAsync($"/history/{promptId}", cancellationToken);
                            if (historyResponse.IsSuccessStatusCode)
                            {
                                string historyJson = await historyResponse.Content.ReadAsStringAsync(cancellationToken);
                                JsonDocument historyDoc = JsonDocument.Parse(historyJson);
                                
                                // History structure: { prompt_id: { status, outputs, error, ... } }
                                if (historyDoc.RootElement.TryGetProperty(promptId, out JsonElement historyEntry))
                                {
                                    // Check for error status
                                    if (historyEntry.TryGetProperty("status", out JsonElement statusElem))
                                    {
                                        string status = statusElem.GetString() ?? "";
                                        if (status.ToLower() == "error" || status.ToLower() == "failed")
                                        {
                                            progress?.Report("Workflow execution failed. Check ComfyUI console for details.");
                                            return false;
                                        }
                                    }
                                    
                                    // Check for error messages
                                    if (historyEntry.TryGetProperty("error", out JsonElement errorElem))
                                    {
                                        string errorMessage = errorElem.GetString() ?? "Unknown error";
                                        progress?.Report($"Workflow error: {errorMessage}");
                                        return false;
                                    }
                                    
                                    // Check if there are outputs (successful completion)
                                    if (historyEntry.TryGetProperty("outputs", out JsonElement outputs))
                                    {
                                        return true; // Workflow completed successfully
                                    }
                                }
                                
                                // If we can't find the entry, assume it completed (it's not in queue)
                                return true;
                            }
                        }
                    }
                    
                    progress?.Report($"Waiting for workflow to complete... (ID: {promptId})");
                    await Task.Delay(pollIntervalMs, cancellationToken);
                }
                catch (HttpRequestException httpEx)
                {
                    // Server connection lost - check if we can reconnect
                    progress?.Report($"Connection to ComfyUI server lost: {httpEx.Message}");
                    lastError = httpEx.Message;
                    
                    // Try to verify server is still accessible
                    bool stillAccessible = await IsServerAccessibleAsync(cancellationToken);
                    if (!stillAccessible)
                    {
                        throw new Exception($"ComfyUI server became unresponsive during workflow execution.\n\n" +
                                           $"The workflow may still be running in ComfyUI, but we can no longer check its status.\n\n" +
                                           $"Please check ComfyUI's interface to see if the workflow completed.\n\n" +
                                           $"Connection error: {httpEx.Message}");
                    }
                    
                    // Server is accessible again, continue polling
                    await Task.Delay(pollIntervalMs, cancellationToken);
                }
                catch (TaskCanceledException timeoutEx) when (timeoutEx.InnerException is TimeoutException)
                {
                    // Request timeout - server might be busy or slow
                    progress?.Report($"Request timeout while checking status. Server may be busy processing the workflow...");
                    lastError = "Request timeout";
                    await Task.Delay(pollIntervalMs * 2, cancellationToken); // Wait longer before retrying
                }
                catch (Exception ex)
                {
                    // If it's a workflow error exception, re-throw it
                    if (ex.Message.Contains("Workflow error") || ex.Message.Contains("Workflow execution failed"))
                    {
                        throw;
                    }
                    
                    progress?.Report($"Error checking status: {ex.Message}");
                    lastError = ex.Message;
                    await Task.Delay(pollIntervalMs, cancellationToken);
                }
            }

            return false; // Cancelled
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class ExecutionResult
    {
        public bool Success { get; set; }
        public string? PromptId { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

