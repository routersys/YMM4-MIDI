using System.Text.Json;
using WorldlineHost;

if (Console.IsInputRedirected || Console.IsOutputRedirected)
{
    RunIpcLoop();
}
else
{
    ShowRichInfoScreen();
}

static void RunIpcLoop()
{
    try
    {
        Logger.Info("WorldlineHost started in IPC mode. Waiting for requests...");

        using (var stdin = Console.OpenStandardInput())
        using (var reader = new StreamReader(stdin, Console.InputEncoding, false, 1024, true))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                Logger.Info($"Received request ({line.Length} chars).");
                SynthResponse response;
                try
                {
                    var request = JsonSerializer.Deserialize<SynthRequestData>(line, IpcJsonContext.Default.SynthRequestData);
                    if (request == null)
                    {
                        throw new InvalidOperationException("Failed to deserialize request.");
                    }

                    response = SynthHandler.Synthesize(request);
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to process request.", ex);
                    response = new SynthResponse { Success = false, ErrorMessage = ex.ToString() };
                }

                try
                {
                    string jsonResponse = JsonSerializer.Serialize(response, IpcJsonContext.Default.SynthResponse);
                    Console.WriteLine(jsonResponse);
                    Logger.Info("Sent response.");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to serialize or send response.", ex);
                }
            }
        }
        Logger.Info("Stdin closed. Exiting IPC mode.");
    }
    catch (Exception ex)
    {
        Logger.Error("Unhandled exception in IPC loop. Exiting.", ex);
    }
}

static void ShowRichInfoScreen()
{
    Console.Title = "Worldline Host Process";

    try
    {
        int windowWidth = Console.WindowWidth;
        if (windowWidth < 70)
        {
            ShowSimpleInfoScreen($"コンソール幅 ({windowWidth}) が狭すぎます。70 以上必要です。");
            return;
        }

        Console.Clear();
        Console.ResetColor();

        int infoWidth = 60;
        int innerWidth = infoWidth + 2;
        string infoPadding = new string(' ', (windowWidth - (infoWidth + 4)) / 2);

        Console.WriteLine("\n");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(infoPadding + "┌" + new string('─', innerWidth) + "┐");

        Console.Write(infoPadding + "│");
        Console.Write(" ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Worldline Host Process");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(PadRight("", innerWidth - 1 - "Worldline Host Process".Length) + "│");

        Console.WriteLine(infoPadding + "├" + new string('─', innerWidth) + "┤");
        Console.WriteLine(infoPadding + "│" + PadRight("", innerWidth) + "│");

        Console.Write(infoPadding + "│");
        Console.Write("   ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("Status:".PadRight(12));
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("Offline (Standalone Mode)");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(PadRight("", innerWidth - 3 - 12 - "Offline (Standalone Mode)".Length) + "│");

        Console.Write(infoPadding + "│");
        Console.Write("   ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("Mode:".PadRight(12));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Direct Execution (Not IPC)");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(PadRight("", innerWidth - 3 - 12 - "Direct Execution (Not IPC)".Length) + "│");

        Console.Write(infoPadding + "│");
        Console.Write("   ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("Version:".PadRight(12));
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("1.0.0.0");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(PadRight("", innerWidth - 3 - 12 - "1.0.0.0".Length) + "│");

        Console.WriteLine(infoPadding + "│" + PadRight("", innerWidth) + "│");
        Console.WriteLine(infoPadding + "└" + new string('─', innerWidth) + "┘");
        Console.WriteLine("\n");
        Console.ResetColor();

        string line1 = "This process is a helper for the YMM4 MIDI Plugin.";
        string line2 = "It runs in the background when YMM4 is synthesizing.";
        string line3 = "Running this .exe directly has no effect.";

        Console.WriteLine(new string(' ', (windowWidth - line1.Length) / 2) + line1);
        Console.WriteLine(new string(' ', (windowWidth - line2.Length) / 2) + line2);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string(' ', (windowWidth - line3.Length) / 2) + line3);
        Console.ResetColor();

        Console.WriteLine("\n\n");
        string footerText = "Press any key to exit this screen.";
        string footerPadding = new string(' ', (windowWidth - footerText.Length) / 2);
        Console.WriteLine(footerPadding + footerText);

        Console.ReadKey();
    }
    catch (Exception)
    {
        ShowSimpleInfoScreen("Failed to render rich UI (window size query failed).");
    }
}

static void ShowSimpleInfoScreen(string message)
{
    Console.Clear();
    Console.WriteLine("Worldline Host Process");
    Console.WriteLine("------------------------");
    Console.WriteLine(message);
    Console.WriteLine("\nThis process is a helper for the YMM4 MIDI Plugin.");
    Console.WriteLine("Press any key to exit.");
    try
    {
        Console.ReadKey();
    }
    catch { }
}

static string PadRight(string text, int width)
{
    if (width <= 0) return "";
    if (text.Length >= width)
    {
        return text.Substring(0, width);
    }
    return text + new string(' ', width - text.Length);
}