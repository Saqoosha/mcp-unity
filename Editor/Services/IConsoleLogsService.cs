using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace McpUnity.Services
{
    /// <summary>
    /// Interface for the console logs service
    /// </summary>
    public interface IConsoleLogsService
    {
        /// <summary>
        /// Get logs as a JSON object with pagination support
        /// </summary>
        /// <param name="logType">Filter by log type (empty for all)</param>
        /// <param name="offset">Starting index (0-based)</param>
        /// <param name="limit">Maximum number of logs to return (default: 100)</param>
        /// <param name="includeStackTrace">Whether to include stack trace in logs (default: true)</param>
        /// <returns>JObject containing logs array and pagination info</returns>
        JObject GetLogsAsJson(string logType = "", int offset = 0, int limit = 100, bool includeStackTrace = true);
        
        /// <summary>
        /// Start listening for logs
        /// </summary>
        void StartListening();
        
        /// <summary>
        /// Stop listening for logs
        /// </summary>
        void StopListening();
        
        /// <summary>
        /// Manually clean up old log entries, keeping only the most recent ones
        /// </summary>
        /// <param name="keepCount">Number of recent entries to keep (default: 500)</param>
        void CleanupOldLogs(int keepCount = 500);
        
        /// <summary>
        /// Get current log count
        /// </summary>
        /// <returns>Number of stored log entries</returns>
        int GetLogCount();
        
        /// <summary>
        /// Search logs with keyword or regex pattern
        /// </summary>
        /// <param name="keyword">Keyword to search for (partial match)</param>
        /// <param name="regex">Regular expression pattern (overrides keyword if provided)</param>
        /// <param name="logType">Filter by log type (empty for all)</param>
        /// <param name="includeStackTrace">Whether to include stack trace in search (default: true)</param>
        /// <param name="caseSensitive">Whether the search is case sensitive (default: false)</param>
        /// <param name="offset">Starting index (0-based)</param>
        /// <param name="limit">Maximum number of logs to return (default: 50)</param>
        /// <returns>JObject containing matching logs array and pagination info</returns>
        JObject SearchLogsAsJson(string keyword = null, string regex = null, string logType = null, 
            bool includeStackTrace = true, bool caseSensitive = false, int offset = 0, int limit = 50);
    }
}
