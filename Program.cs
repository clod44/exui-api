using System;
using System.IO;
using System.Globalization;
using System.Threading.Tasks;

namespace ExuiApi;

class Program
{
    static async Task Main(string[] args)
    {
        ConfigureConsole();
        LoadDefinitions();
        ExuiWebServer.Initialize();
        await GameDataReader.RunMainLoop();
    }

    private static void ConfigureConsole()
    {
        Console.Title = "exui API Provider";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================================================");
        Console.WriteLine("          EXUI - EXTERNAL UI API PROVIDER         ");
        Console.WriteLine("==================================================");
        Console.ResetColor();
    }

    private static void LoadDefinitions()
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string targetFile = Path.Combine(baseDirectory, "variables.txt");

        if (!File.Exists(targetFile))
        {
            try
            {
                string defaultTemplate = "# exui Telemetry Variable Mappings\n# Format: name,type,module+baseOffset[->pointerOffset1...]\n\nspeed,float,speed2.exe+3F09E8\ngear,int,speed2.exe+4659BC->1E4\n";
                File.WriteAllText(targetFile, defaultTemplate);
                Console.WriteLine($"[exui] '{targetFile}' not found. Created a clean configuration blueprint file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[exui] Critical Error creating configuration file: {ex.Message}");
            }
            return;
        }

        int loadCount = 0;
        string[] lines = File.ReadAllLines(targetFile);

        foreach (string line in lines)
        {
            // Skip comments and blank lines completely
            if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#")) continue;

            try
            {
                string[] parts = line.Split(',');
                if (parts.Length < 3)
                {
                    Console.WriteLine($"[exui] Skipping malformed line (missing columns): {line}");
                    continue;
                }

                string name = parts[0].Trim();
                string type = parts[1].Trim().ToLower();
                string memoryPath = parts[2].Trim(); // "speed2.exe+4659BC->1E4"

                // Step 1: Delimit out any optional dynamic pointer chasing trails
                string[] pathSegments = memoryPath.Split("->");

                // Step 2: Unpack the base offset from segment zero
                string[] baseParts = pathSegments[0].Split('+');
                if (baseParts.Length < 2)
                {
                    Console.WriteLine($"[exui] Skipping bad path configuration formatting: {pathSegments[0]}");
                    continue;
                }

                string moduleName = baseParts[0].Trim();
                uint baseOffset = uint.Parse(baseParts[1].Trim(), NumberStyles.HexNumber);

                // Step 3: Unroll any subsequent recursive deep memory jumps
                uint[] pointerOffsets = new uint[pathSegments.Length - 1];
                for (int i = 1; i < pathSegments.Length; i++)
                {
                    pointerOffsets[i - 1] = uint.Parse(pathSegments[i].Trim(), NumberStyles.HexNumber);
                }

                // Step 4: Register complete structural template mapping profile
                var def = new VariableDefinition
                {
                    Name = name,
                    Type = type,
                    ModuleName = moduleName,
                    BaseOffset = baseOffset,
                    PointerOffsets = pointerOffsets
                };

                GameState.Definitions.Add(def);
                GameState.Telemetry[def.Name] = 0;
                loadCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[exui] Failed parsing item line [{line}]: {ex.Message}");
            }
        }

        Console.WriteLine($"[exui] Successfully mapped and tracking {loadCount} telemetry data targets.");
        Console.WriteLine("--------------------------------------------------");
    }
}