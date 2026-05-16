#pragma warning disable CA1416

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Reloaded.Memory;

namespace ExuiApi;

public static class GameDataReader
{
    public static async Task RunMainLoop()
    {
        while (true)
        {
            Process? gameProcess = GetGameProcess();

            if (gameProcess == null)
            {
                HandleGameMissing();
                await Task.Delay(1000);
                continue;
            }

            HandleGameFound(gameProcess);
            ProcessGameMemory(gameProcess);
            await Task.Delay(16);
        }
    }

    private static Process? GetGameProcess()
    {
        Process[] processes = Process.GetProcessesByName("speed2");
        return processes.Length > 0 ? processes[0] : null;
    }

    private static void HandleGameMissing()
    {
        if (GameState.IsGameRunning)
        {
            Console.WriteLine("[exui] Game closed or lost focus. Reverting to scan mode...");
            GameState.IsGameRunning = false;
            foreach (var key in GameState.Telemetry.Keys)
            {
                GameState.Telemetry[key] = 0;
            }
        }
    }

    private static void HandleGameFound(Process gameProcess)
    {
        if (!GameState.IsGameRunning)
        {
            Console.WriteLine($"[exui] Linked successfully to NFSU2! (PID: {gameProcess.Id})");
            GameState.IsGameRunning = true;
        }
    }

    private static void ProcessGameMemory(Process gameProcess)
    {
        try
        {
            ExternalMemory memoryBridge = new ExternalMemory(gameProcess);

            foreach (var def in GameState.Definitions)
            {
                IntPtr baseAddress = GetModuleBaseAddress(gameProcess, def.ModuleName);
                if (baseAddress == IntPtr.Zero) continue;

                // 1. Point initially to the relative module base location pointer
                nuint resolvedAddress = (nuint)((nint)baseAddress + (nint)def.BaseOffset);
                bool resolutionSuccess = true;

                // 2. Pointer Chasing: Trace the offsets into heap memory if a chain exists
                if (def.PointerOffsets != null && def.PointerOffsets.Length > 0)
                {
                    foreach (uint offset in def.PointerOffsets)
                    {
                        try
                        {
                            // Read the 4-byte destination address held inside the current register pointer
                            memoryBridge.Read<uint>(resolvedAddress, out uint nextPointer);
                            
                            // Jump to the retrieved target address and apply the next structural shift offset
                            resolvedAddress = (nuint)nextPointer + (nuint)offset;
                        }
                        catch
                        {
                            // If memory is unreadable (e.g. game is loading), flag failure and drop out of this chain
                            resolutionSuccess = false;
                            break;
                        }
                    }
                }

                // Safety drop: Break out if a pointer resolved to null or failed reading boundaries
                if (!resolutionSuccess || resolvedAddress == 0) continue;

                // 3. Extract final data values from the completely dereferenced address path
                switch (def.Type)
                {
                    case "float":
                        memoryBridge.Read<float>(resolvedAddress, out float fValue);
                        GameState.Telemetry[def.Name] = fValue < 0 ? 0.0f : Math.Round(fValue, 2);
                        break;

                    case "byte":
                        memoryBridge.Read<byte>(resolvedAddress, out byte bValue);
                        GameState.Telemetry[def.Name] = bValue;
                        break;

                    case "int":
                    case "4 bytes": // Gracefully matches standard Cheat Engine terminology
                        memoryBridge.Read<int>(resolvedAddress, out int iValue);
                        GameState.Telemetry[def.Name] = iValue;
                        break;
                }
            }
        }
        catch
        {
            // Suppresses context execution errors if the game is loading files or changing states
        }
    }

    private static IntPtr GetModuleBaseAddress(Process process, string moduleName)
    {
        foreach (ProcessModule module in process.Modules)
        {
            if (string.Equals(module.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
            {
                return module.BaseAddress;
            }
        }
        return IntPtr.Zero;
    }
}