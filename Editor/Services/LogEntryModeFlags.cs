namespace McpUnity.Services
{
    /// <summary>
    /// Unity LogEntry mode flags constants
    /// Based on Unity's internal LogMessageFlags from UnityCsReference
    /// https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/LogEntries.bindings.cs
    /// </summary>
    public static class LogEntryModeFlags
    {
        // Basic log type flags (bits 0-4)
        public const int kModeError = 1 << 0;         // 1     (0x1)
        public const int kModeAssert = 1 << 1;        // 2     (0x2)
        public const int kModeLog = 1 << 2;           // 4     (0x4)
        public const int kModeWarning = 1 << 3;       // 8     (0x8)
        public const int kModeException = 1 << 4;     // 16    (0x10)
        
        // Scripting related flags (bits 8-12)
        public const int kScriptingError = 1 << 8;    // 256   (0x100)
        public const int kScriptingWarning = 1 << 9;  // 512   (0x200)
        public const int kScriptingLog = 1 << 10;     // 1024  (0x400)
        public const int kScriptCompileError = 1 << 11;   // 2048  (0x800)
        public const int kScriptCompileWarning = 1 << 12; // 4096  (0x1000)
        
        // Observed composite values from debugging
        // These include additional undocumented high bits
        public const int ObservedCompilerWarning = 266240; // 0x41000 (bits: 18, 10)
        public const int ObservedCompilerError = 272384;   // 0x42800 (bits: 18, 14, 10)
        public const int ObservedShaderError = 262212;     // 0x40044 (bits: 18, 6, 2)
        public const int ObservedRuntimeWarning = 8405504; // 0x804200 (bits: 23, 18, 9)
        public const int ObservedRuntimeError = 8405248;   // 0x804100 (bits: 23, 18, 8)
        
        /// <summary>
        /// Determine log type from mode flags
        /// </summary>
        public static string GetLogTypeFromMode(int mode)
        {
            // Check for observed compiler/shader message patterns first
            if (mode == ObservedCompilerError) return "Error";
            if (mode == ObservedCompilerWarning) return "Warning";
            if (mode == ObservedShaderError) return "Error";
            
            // Check for script compile errors/warnings
            if ((mode & kScriptCompileError) != 0) return "Error";
            if ((mode & kScriptCompileWarning) != 0) return "Warning";
            
            // Check for scripting errors/warnings
            if ((mode & kScriptingError) != 0) return "Error";
            if ((mode & kScriptingWarning) != 0) return "Warning";
            
            // Then check standard flags
            if ((mode & kModeError) != 0) return "Error";
            if ((mode & kModeAssert) != 0) return "Assert";
            if ((mode & kModeException) != 0) return "Exception";
            if ((mode & kModeWarning) != 0) return "Warning";
            if ((mode & kModeLog) != 0) return "Log";
            
            return "Log"; // Default to Log instead of Unknown
        }
    }
}