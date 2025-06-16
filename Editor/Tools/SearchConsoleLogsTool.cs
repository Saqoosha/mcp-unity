using System;
using Newtonsoft.Json.Linq;
using McpUnity.Services;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for searching Unity console logs with keyword or regex pattern
    /// </summary>
    public class SearchConsoleLogsTool : McpToolBase
    {
        private readonly IConsoleLogsService _consoleLogsService;

        public SearchConsoleLogsTool(IConsoleLogsService consoleLogsService)
        {
            Name = "search_console_logs";
            Description = "Search Unity console logs with keyword or regex pattern. Supports case-sensitive/insensitive search, optional stack trace inclusion, and pagination. Regex takes precedence over keyword if both are provided.";
            
            _consoleLogsService = consoleLogsService;
        }

        /// <summary>
        /// Execute the search console logs tool
        /// </summary>
        /// <param name="parameters">Tool parameters</param>
        /// <returns>JObject containing search results</returns>
        public override JObject Execute(JObject parameters)
        {
            // Extract search parameters
            string keyword = parameters?["keyword"]?.ToString();
            string regex = parameters?["regex"]?.ToString();
            string logType = parameters?["logType"]?.ToString();
            
            bool includeStackTrace = GetBoolParameter(parameters, "includeStackTrace", true);
            bool caseSensitive = GetBoolParameter(parameters, "caseSensitive", false);
            int offset = Math.Max(0, GetIntParameter(parameters, "offset", 0));
            int limit = Math.Max(1, Math.Min(1000, GetIntParameter(parameters, "limit", 50)));
            
            // At least one search criteria must be provided
            if (string.IsNullOrEmpty(keyword) && string.IsNullOrEmpty(regex))
            {
                return new JObject
                {
                    ["logs"] = new JArray(),
                    ["error"] = "Either 'keyword' or 'regex' parameter must be provided",
                    ["success"] = false
                };
            }
            
            // Call the search method
            JObject result = _consoleLogsService.SearchLogsAsJson(
                keyword, regex, logType, includeStackTrace, caseSensitive, offset, limit);
            
            // Add formatted message if search was successful
            if (result["success"]?.Value<bool>() == true)
            {
                string searchTerm = !string.IsNullOrEmpty(regex) ? $"regex '{regex}'" : $"keyword '{keyword}'";
                string typeFilter = !string.IsNullOrEmpty(logType) ? $" of type '{logType}'" : "";
                int returnedCount = result["_returnedCount"]?.Value<int>() ?? 0;
                int matchedCount = result["_matchedCount"]?.Value<int>() ?? 0;
                int filteredCount = result["_filteredCount"]?.Value<int>() ?? 0;
                int totalCount = result["_totalCount"]?.Value<int>() ?? 0;
                
                result["message"] = $"Found {matchedCount} logs matching {searchTerm}{typeFilter} (returned: {returnedCount}, filtered by type: {filteredCount}, total: {totalCount})";
                
                // Remove internal count fields (they're now in the message)
                result.Remove("_totalCount");
                result.Remove("_filteredCount");
                result.Remove("_matchedCount");
                result.Remove("_returnedCount");
            }

            return result;
        }

        /// <summary>
        /// Helper method to safely extract integer parameters with default values
        /// </summary>
        private static int GetIntParameter(JObject parameters, string key, int defaultValue)
        {
            if (parameters?[key] != null && int.TryParse(parameters[key].ToString(), out int value))
                return value;
            return defaultValue;
        }

        /// <summary>
        /// Helper method to safely extract boolean parameters with default values
        /// </summary>
        private static bool GetBoolParameter(JObject parameters, string key, bool defaultValue)
        {
            if (parameters?[key] != null && bool.TryParse(parameters[key].ToString(), out bool value))
                return value;
            return defaultValue;
        }
    }
}