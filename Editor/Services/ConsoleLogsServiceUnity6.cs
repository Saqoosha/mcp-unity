using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Services
{
    /// <summary>
    /// Unity 6 specific implementation using ConsoleWindowUtility API
    /// This implementation uses Unity's internal console APIs for more reliable log retrieval
    /// </summary>
    public class ConsoleLogsServiceUnity6 : IConsoleLogsService
    {
        // Static mapping for MCP log types to Unity log types
        private static readonly Dictionary<string, HashSet<string>> LogTypeMapping = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "info", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Log" } },
            { "error", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Error", "Exception", "Assert" } },
            { "warning", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Warning" } }
        };
        
        // Reflection cache for internal Unity APIs
        private static Type _logEntriesType;
        private static MethodInfo _getCountMethod;
        private static MethodInfo _getEntryInternalMethod;
        private static MethodInfo _startGettingEntriesMethod;
        private static MethodInfo _endGettingEntriesMethod;
        private static Type _logEntryType;
        private static FieldInfo _messageField;
        private static FieldInfo _fileField;
        private static FieldInfo _lineField;
        private static FieldInfo _modeField;
        private static FieldInfo _callstackTextStartUTF8Field;
        private static FieldInfo _callstackTextStartUTF16Field;
        
        static ConsoleLogsServiceUnity6()
        {
            InitializeReflection();
        }
        
        private static void InitializeReflection()
        {
            // Get LogEntries type
            _logEntriesType = Type.GetType("UnityEditor.LogEntries,UnityEditor");
            if (_logEntriesType == null)
            {
                Debug.LogError("[MCP Unity] Failed to find LogEntries type");
                return;
            }
            
            // Get LogEntry type
            _logEntryType = Type.GetType("UnityEditor.LogEntry,UnityEditor");
            if (_logEntryType == null)
            {
                Debug.LogError("[MCP Unity] Failed to find LogEntry type");
                return;
            }
            
            // Get methods
            _getCountMethod = _logEntriesType.GetMethod("GetCount",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            
            _startGettingEntriesMethod = _logEntriesType.GetMethod("StartGettingEntries",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                
            _endGettingEntriesMethod = _logEntriesType.GetMethod("EndGettingEntries",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                
            _getEntryInternalMethod = _logEntriesType.GetMethod("GetEntryInternal",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            
            // Get fields
            _messageField = _logEntryType.GetField("message", BindingFlags.Public | BindingFlags.Instance);
            _fileField = _logEntryType.GetField("file", BindingFlags.Public | BindingFlags.Instance);
            _lineField = _logEntryType.GetField("line", BindingFlags.Public | BindingFlags.Instance);
            _modeField = _logEntryType.GetField("mode", BindingFlags.Public | BindingFlags.Instance);
            _callstackTextStartUTF8Field = _logEntryType.GetField("callstackTextStartUTF8", BindingFlags.Public | BindingFlags.Instance);
            _callstackTextStartUTF16Field = _logEntryType.GetField("callstackTextStartUTF16", BindingFlags.Public | BindingFlags.Instance);
        }
        
        public void StartListening()
        {
            // Unity 6: Register for console changes
            ConsoleWindowUtility.consoleLogsChanged += OnConsoleLogsChanged;
        }
        
        public void StopListening()
        {
            // Unity 6: Unregister from console changes
            ConsoleWindowUtility.consoleLogsChanged -= OnConsoleLogsChanged;
        }
        
        private void OnConsoleLogsChanged()
        {
            // This is called whenever console logs change:
            // - New logs added
            // - Console cleared
            // - Logs filtered/collapsed
            
            ConsoleWindowUtility.GetConsoleLogCounts(out int error, out int warning, out int log);
            int totalLogs = error + warning + log;
            
            if (totalLogs == 0)
            {
                Debug.Log("[MCP Unity] Console cleared detected via Unity 6 API");
            }
            
            // Since we query Unity directly, we don't need to maintain our own cache
            // This event just helps us know when to notify clients that log state changed
        }
        
        public JObject GetLogsAsJson(string logType = "", int offset = 0, int limit = 100, bool includeStackTrace = true)
        {
            if (_logEntriesType == null || _getCountMethod == null || _getEntryInternalMethod == null)
            {
                return new JObject
                {
                    ["logs"] = new JArray(),
                    ["message"] = "LogEntries API not available",
                    ["success"] = false
                };
            }
            
            JArray logsArray = new JArray();
            
            try
            {
                // Get console log counts using Unity 6 API
                ConsoleWindowUtility.GetConsoleLogCounts(out int errorCount, out int warningCount, out int logCount);
                int totalCount = errorCount + warningCount + logCount;
                
                // Map MCP log types to Unity log types
                HashSet<string> unityLogTypes = null;
                bool filter = !string.IsNullOrEmpty(logType);
                if (filter)
                {
                    if (LogTypeMapping.TryGetValue(logType, out var mapped))
                    {
                        unityLogTypes = mapped;
                    }
                    else
                    {
                        unityLogTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { logType };
                    }
                }
                
                // Start getting entries
                _startGettingEntriesMethod?.Invoke(null, null);
                
                int currentCount = (int)_getCountMethod.Invoke(null, null);
                var collectedLogs = new List<JObject>();
                
                // Iterate through all logs (newest first)
                for (int i = currentCount - 1; i >= 0; i--)
                {
                    // Create LogEntry instance
                    var logEntry = Activator.CreateInstance(_logEntryType);
                    
                    // GetEntryInternal(int row, LogEntry outputEntry)
                    bool success = (bool)_getEntryInternalMethod.Invoke(null, new object[] { i, logEntry });
                    
                    if (!success) continue;
                    
                    // Extract fields (see Unity6InternalAPIReference.md for field details)
                    string fullMessage = _messageField?.GetValue(logEntry) as string ?? "";
                    string file = _fileField?.GetValue(logEntry) as string ?? "";
                    int line = _lineField?.GetValue(logEntry) as int? ?? 0;
                    int mode = _modeField?.GetValue(logEntry) as int? ?? 0;
                    int callstackStartUTF8 = _callstackTextStartUTF8Field?.GetValue(logEntry) as int? ?? 0;
                    int callstackStartUTF16 = _callstackTextStartUTF16Field?.GetValue(logEntry) as int? ?? 0;
                    
                    // Debug: Write mode values to file for analysis (disabled by default)
                    #if MCP_UNITY_DEBUG_MODE_VALUES
                    if (fullMessage.Contains("error") || fullMessage.Contains("Error") || 
                        fullMessage.Contains("warning") || fullMessage.Contains("Warning") ||
                        fullMessage.Contains("failed") || fullMessage.Contains("Failed") ||
                        fullMessage.Contains("exception") || fullMessage.Contains("Exception"))
                    {
                        WriteDebugInfo(fullMessage, mode);
                    }
                    #endif
                    
                    // Parse message and stack trace using Unity's internal callstack position (prefer UTF-16)
                    var (actualMessage, stackTrace) = ParseMessageAndStackTrace(fullMessage, callstackStartUTF16, callstackStartUTF8);
                    
                    // Determine log type from mode and stack trace
                    string entryType = DetermineLogTypeFromModeAndContent(mode, stackTrace);
                    
                    // Skip if filtering and doesn't match
                    if (filter && !unityLogTypes.Contains(entryType))
                        continue;
                    
                    // Create log object
                    var logObject = new JObject
                    {
                        ["message"] = actualMessage,
                        ["type"] = entryType,
                        ["timestamp"] = DateTime.Now.AddSeconds(-(currentCount - i)).ToString("yyyy-MM-dd HH:mm:ss.fff")
                    };
                    
                    // Include stack trace if requested
                    if (includeStackTrace && !string.IsNullOrEmpty(stackTrace))
                    {
                        logObject["stackTrace"] = stackTrace;
                    }
                    
                    collectedLogs.Add(logObject);
                }
                
                // End getting entries
                _endGettingEntriesMethod?.Invoke(null, null);
                
                // Apply pagination
                var paginatedLogs = collectedLogs
                    .Skip(offset)
                    .Take(limit)
                    .ToList();
                
                foreach (var log in paginatedLogs)
                {
                    logsArray.Add(log);
                }
                
                return new JObject
                {
                    ["logs"] = logsArray,
                    ["_totalCount"] = totalCount,
                    ["_filteredCount"] = collectedLogs.Count,
                    ["_returnedCount"] = paginatedLogs.Count,
                    ["message"] = $"Retrieved {paginatedLogs.Count} of {collectedLogs.Count} log entries (offset: {offset}, limit: {limit}, total: {totalCount})",
                    ["success"] = true
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Unity] Error getting logs: {ex.Message}");
                return new JObject
                {
                    ["logs"] = new JArray(),
                    ["message"] = $"Error retrieving logs: {ex.Message}",
                    ["success"] = false
                };
            }
        }
        
        private (string message, string stackTrace) ParseMessageAndStackTrace(string fullMessage, int callstackStartUTF16, int callstackStartUTF8)
        {
            if (string.IsNullOrEmpty(fullMessage))
                return ("", "");

            // Try UTF-16 position first (C# strings are UTF-16)
            if (callstackStartUTF16 > 0 && callstackStartUTF16 < fullMessage.Length)
            {
                try
                {
                    string message = fullMessage.Substring(0, callstackStartUTF16).TrimEnd('\n', '\r');
                    string stackTrace = fullMessage.Substring(callstackStartUTF16);
                    return (message, stackTrace);
                }
                catch
                {
                    // Continue to next attempt
                }
            }

            // Fallback to UTF-8 position
            if (callstackStartUTF8 > 0 && callstackStartUTF8 < fullMessage.Length)
            {
                try
                {
                    string message = fullMessage.Substring(0, callstackStartUTF8).TrimEnd('\n', '\r');
                    string stackTrace = fullMessage.Substring(callstackStartUTF8);
                    return (message, stackTrace);
                }
                catch
                {
                    // Continue to heuristic parsing
                }
            }

            // Fallback: heuristic parsing (previous method)
            var lines = fullMessage.Split(new[] { '\n' }, StringSplitOptions.None);
            
            if (lines.Length == 1)
            {
                return (fullMessage, "");
            }
            
            // Find the first line that looks like a stack trace
            int stackTraceStartIndex = -1;
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("UnityEngine.") || 
                    line.StartsWith("System.") ||
                    line.Contains(" (at ") ||
                    line.Contains(":") && (line.Contains("(") && line.Contains(")")))
                {
                    stackTraceStartIndex = i;
                    break;
                }
            }
            
            if (stackTraceStartIndex == -1)
            {
                return (fullMessage, "");
            }
            
            string fallbackMessage = string.Join("\n", lines.Take(stackTraceStartIndex));
            string fallbackStackTrace = string.Join("\n", lines.Skip(stackTraceStartIndex));
            
            return (fallbackMessage, fallbackStackTrace);
        }
        
        private string DetermineLogTypeFromModeAndContent(int mode, string stackTrace)
        {
            // First try to determine from stack trace content
            if (!string.IsNullOrEmpty(stackTrace))
            {
                if (stackTrace.Contains("UnityEngine.Debug:LogError") ||
                    stackTrace.Contains("UnityEngine.Logger:LogError"))
                    return "Error";
                    
                if (stackTrace.Contains("UnityEngine.Debug:LogWarning") ||
                    stackTrace.Contains("UnityEngine.Logger:LogWarning"))
                    return "Warning";
                    
                if (stackTrace.Contains("UnityEngine.Debug:LogException") ||
                    stackTrace.Contains("UnityEngine.Logger:LogException"))
                    return "Exception";
                    
                if (stackTrace.Contains("UnityEngine.Debug:LogAssertion") ||
                    stackTrace.Contains("UnityEngine.Assertions.Assert"))
                    return "Assert";
                    
                if (stackTrace.Contains("UnityEngine.Debug:Log"))
                    return "Log";
            }
            
            // Fallback to mode flags
            return GetLogTypeFromMode(mode);
        }
        
        private string GetLogTypeFromMode(int mode)
        {
            // Use centralized mode flags logic
            return LogEntryModeFlags.GetLogTypeFromMode(mode);
        }
        
        public void CleanupOldLogs(int keepCount = 500)
        {
            // Not needed for Unity 6 implementation as we query directly from Unity
        }
        
        public int GetLogCount()
        {
            ConsoleWindowUtility.GetConsoleLogCounts(out int error, out int warning, out int log);
            return error + warning + log;
        }
        
        /// <summary>
        /// Search logs with keyword or regex pattern
        /// </summary>
        public JObject SearchLogsAsJson(string keyword = null, string regex = null, string logType = null, 
            bool includeStackTrace = true, bool caseSensitive = false, int offset = 0, int limit = 50)
        {
            // Prepare search criteria
            bool hasSearchCriteria = !string.IsNullOrEmpty(keyword) || !string.IsNullOrEmpty(regex);
            Regex searchRegex = null;
            string searchKeyword = keyword;
            
            // If regex is provided, use it instead of keyword
            if (!string.IsNullOrEmpty(regex))
            {
                try
                {
                    searchRegex = new Regex(regex, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                }
                catch (ArgumentException ex)
                {
                    return new JObject
                    {
                        ["logs"] = new JArray(),
                        ["error"] = $"Invalid regex pattern: {ex.Message}",
                        ["success"] = false
                    };
                }
            }
            else if (!string.IsNullOrEmpty(keyword) && !caseSensitive)
            {
                searchKeyword = keyword.ToLower();
            }
            
            // Map MCP log types to Unity log types
            HashSet<string> unityLogTypes = null;
            if (!string.IsNullOrEmpty(logType))
            {
                if (LogTypeMapping.TryGetValue(logType, out var mapped))
                {
                    unityLogTypes = mapped;
                }
                else
                {
                    unityLogTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { logType };
                }
            }
            
            JArray logsArray = new JArray();
            int totalCount = 0;
            int filteredCount = 0;
            int matchedCount = 0;
            int currentIndex = 0;
            
            // Get total count using reflection
            try
            {
                totalCount = (int)_getCountMethod.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Unity] Error getting log count: {ex.Message}");
                return new JObject
                {
                    ["logs"] = logsArray,
                    ["error"] = "Failed to access Unity console logs",
                    ["success"] = false
                };
            }
            
            if (totalCount == 0)
            {
                return new JObject
                {
                    ["logs"] = logsArray,
                    ["_totalCount"] = 0,
                    ["_filteredCount"] = 0,
                    ["_matchedCount"] = 0,
                    ["_returnedCount"] = 0,
                    ["success"] = true
                };
            }
            
            try
            {
                // Start getting entries
                _startGettingEntriesMethod?.Invoke(null, null);
                
                // Search through logs (newest first)
                for (int i = totalCount - 1; i >= 0; i--)
                {
                    // Create LogEntry instance
                    var logEntry = Activator.CreateInstance(_logEntryType);
                    
                    // GetEntryInternal(int row, LogEntry outputEntry)
                    bool success = (bool)_getEntryInternalMethod.Invoke(null, new object[] { i, logEntry });
                    
                    if (!success) continue;
                    
                    // Extract fields
                    string fullMessage = _messageField?.GetValue(logEntry) as string ?? "";
                    string file = _fileField?.GetValue(logEntry) as string ?? "";
                    int line = _lineField?.GetValue(logEntry) as int? ?? 0;
                    int mode = _modeField?.GetValue(logEntry) as int? ?? 0;
                    int callstackStartUTF8 = _callstackTextStartUTF8Field?.GetValue(logEntry) as int? ?? 0;
                    int callstackStartUTF16 = _callstackTextStartUTF16Field?.GetValue(logEntry) as int? ?? 0;
                    
                    // Parse message and stack trace
                    var (actualMessage, stackTrace) = ParseMessageAndStackTrace(fullMessage, callstackStartUTF16, callstackStartUTF8);
                    
                    // Determine log type
                    string entryLogType = DetermineLogTypeFromModeAndContent(mode, stackTrace);
                    
                    // Skip if filtering by log type and entry doesn't match
                    if (unityLogTypes != null && !unityLogTypes.Contains(entryLogType))
                        continue;
                    
                    filteredCount++;
                    
                    // Check if entry matches search criteria
                    bool matches = true;
                    if (hasSearchCriteria)
                    {
                        matches = false;
                        
                        // Search in message
                        if (searchRegex != null)
                        {
                            matches = searchRegex.IsMatch(actualMessage);
                            if (!matches && includeStackTrace && !string.IsNullOrEmpty(stackTrace))
                            {
                                matches = searchRegex.IsMatch(stackTrace);
                            }
                        }
                        else if (!string.IsNullOrEmpty(searchKeyword))
                        {
                            string messageToSearch = caseSensitive ? actualMessage : actualMessage.ToLower();
                            matches = messageToSearch.Contains(searchKeyword);
                            
                            if (!matches && includeStackTrace && !string.IsNullOrEmpty(stackTrace))
                            {
                                string stackTraceToSearch = caseSensitive ? stackTrace : stackTrace.ToLower();
                                matches = stackTraceToSearch.Contains(searchKeyword);
                            }
                        }
                    }
                    
                    if (!matches) continue;
                    
                    matchedCount++;
                    
                    // Check if we're in the offset range and haven't reached the limit yet
                    if (currentIndex >= offset && logsArray.Count < limit)
                    {
                        var logObject = new JObject
                        {
                            ["message"] = actualMessage,
                            ["type"] = entryLogType,
                            ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                        };
                        
                        // Only include stack trace if requested
                        if (includeStackTrace)
                        {
                            logObject["stackTrace"] = stackTrace;
                        }
                        
                        logsArray.Add(logObject);
                    }
                    
                    currentIndex++;
                    
                    // Early exit if we've collected enough logs
                    if (currentIndex >= offset + limit) break;
                }
            }
            finally
            {
                // End getting entries
                _endGettingEntriesMethod?.Invoke(null, null);
            }
            
            return new JObject
            {
                ["logs"] = logsArray,
                ["_totalCount"] = totalCount,
                ["_filteredCount"] = filteredCount,
                ["_matchedCount"] = matchedCount,
                ["_returnedCount"] = logsArray.Count,
                ["success"] = true
            };
        }
        
        #if MCP_UNITY_DEBUG_MODE_VALUES
        /// <summary>
        /// Debug method to write mode values to file for analysis
        /// Enable by adding MCP_UNITY_DEBUG_MODE_VALUES to Project Settings > Player > Scripting Define Symbols
        /// </summary>
        private void WriteDebugInfo(string message, int mode)
        {
            try
            {
                string debugPath = Path.Combine(Application.dataPath, "..", "mcp-unity-debug.log");
                string debugLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | Mode: {mode} (0x{mode:X}) Binary: {Convert.ToString(mode, 2)} | Message: {message.Substring(0, Math.Min(100, message.Length))}...\n";
                File.AppendAllText(debugPath, debugLine);
            }
            catch
            {
                // Ignore debug write errors
            }
        }
        #endif
    }
}