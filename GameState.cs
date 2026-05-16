using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ExuiApi;
public class VariableDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public uint BaseOffset { get; set; }
    public uint[] PointerOffsets { get; set; } = Array.Empty<uint>();
}
public static class GameState
{
    public static bool IsGameRunning { get; set; } = false;
    public static List<VariableDefinition> Definitions { get; set; } = new();
    public static ConcurrentDictionary<string, object> Telemetry { get; } = new();
}