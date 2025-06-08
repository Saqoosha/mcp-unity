# Unity 6 Internal API Reference

## LogEntry Structure

Unity's internal `LogEntry` structure contains the following fields:

### Fields
| Field Name | Type | Description |
|------------|------|-------------|
| message | String | Full message including stack trace |
| file | String | Source file path |
| line | Int32 | Line number |
| column | Int32 | Column number (-1 if not available) |
| mode | Int32 | Log type flags (see Mode Flags section) |
| instanceID | Int32 | Instance ID of the object |
| identifier | Int32 | Unique identifier |
| globalLineIndex | Int32 | Global line index in console |
| callstackTextStartUTF8 | Int32 | UTF-8 byte position where stack trace starts |
| callstackTextStartUTF16 | Int32 | UTF-16 character position where stack trace starts |

## Mode Flags

### Standard Unity Log Flags (from UnityCsReference)
```csharp
// Basic log types
const int kModeError = 1 << 0;         // 1     (0x1)
const int kModeAssert = 1 << 1;        // 2     (0x2)
const int kModeLog = 1 << 2;           // 4     (0x4)
const int kModeWarning = 1 << 3;       // 8     (0x8)
const int kModeException = 1 << 4;     // 16    (0x10)

// Scripting related flags
const int kScriptingError = 1 << 8;    // 256   (0x100)
const int kScriptingWarning = 1 << 9;  // 512   (0x200)
const int kScriptingLog = 1 << 10;     // 1024  (0x400)
const int kScriptCompileError = 1 << 11;   // 2048  (0x800)
const int kScriptCompileWarning = 1 << 12; // 4096  (0x1000)
```

### Observed Compiler/Shader Message Values (from debugging)
```csharp
// These are composite values with multiple flags set
const int ObservedCompilerWarning = 266240; // 0x41000 (bits: 18, 10)
const int ObservedCompilerError = 272384;   // 0x42800 (bits: 18, 14, 10)
const int ObservedShaderError = 262212;     // 0x40044 (bits: 18, 6, 2)
```

Note: The observed values include additional high bits (2, 6, 10, 14, 18) beyond the documented flags, suggesting Unity may be using undocumented internal flags.

## Important Findings

1. **Compiler messages use special mode values**
   - C# compilation errors (e.g., `error CS0103`) have mode = 272384
   - C# compilation warnings (e.g., `warning CS0414`) have mode = 266240
   - These do NOT use the standard error/warning flags!

2. **Shader errors**
   - Shader compilation errors appear to use standard Log mode (4)
   - Need message content analysis to properly classify

3. **Stack trace separation**
   - Use `callstackTextStartUTF16` for C# strings (preferred)
   - Fallback to `callstackTextStartUTF8` if needed
   - Unity provides exact position where stack trace begins

## Usage Notes

- Always check special compiler flags before standard flags
- Message content analysis may still be needed for some error types
- The mode field alone is not sufficient for all classification needs

## Example Mode Analysis

```
Warning CS0414: mode = 266240 = 0x41000
Binary: 1000001000000000000
Bits set: 18, 10

Error CS0103: mode = 272384 = 0x42800  
Binary: 1000010100000000000
Bits set: 18, 14, 10
```

The high bits (10, 14, 18) appear to indicate compiler-related messages.