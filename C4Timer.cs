using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace C4Timer;

public class C4TimerConfig : BasePluginConfig
{
    [JsonPropertyName("EnableTimer")]
    public bool EnableTimer { get; set; } = true;

    [JsonPropertyName("TimerStarting")]
    public int TimerStarting { get; set; } = 45;

    [JsonPropertyName("EnableProgressBar")]
    public bool EnableProgressBar { get; set; } = true;

    [JsonPropertyName("LeftSideTimer")]
    public string LeftSideTimer { get; set; } = "-[ ";

    [JsonPropertyName("RightSideTimer")]
    public string RightSideTimer { get; set; } = " ]-";
}

public class C4Timer : BasePlugin, IPluginConfig<C4TimerConfig>
{
    public override string ModuleName => "C4 Timer";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleAuthor => "belom0r";

    private bool g_bPlantedC4 = false;
    private float g_flTimerLengthC4 = float.NaN;
    private float g_flTimerСountdownC4 = float.NaN;

    private Timer? g_CountdownToExplosionC4;

    public required C4TimerConfig Config { get; set; }
    public void OnConfigParsed(C4TimerConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventBombPlanted>(BombPlantedPost); //bPlantedC4 = true

        RegisterEventHandler<EventRoundStart>((@event, info) => { g_bPlantedC4 = false; ; return HookResult.Continue;});
        RegisterEventHandler<EventBombExploded>((@event, info) => { g_bPlantedC4 = false; return HookResult.Continue; });
        RegisterEventHandler<EventBombDefused>((@event, info) => { g_bPlantedC4 = false; return HookResult.Continue; });
    }

    private HookResult BombPlantedPost(EventBombPlanted @event, GameEventInfo info)
    {
        var ElementPlantedC4 = GetPlantedC4();

        if (ElementPlantedC4 == null)
        {
            return HookResult.Continue;
        }

        g_bPlantedC4 = true;

        g_flTimerLengthC4 = ElementPlantedC4.TimerLength + 1.0f;
        g_flTimerСountdownC4 = ElementPlantedC4.TimerLength + 1.0f;

        if (Config.TimerStarting > (int)g_flTimerLengthC4 || Config.TimerStarting < 0)
        {
            Config.TimerStarting = (int)g_flTimerLengthC4;
        }

        g_CountdownToExplosionC4 = new Timer(1.0f, CountdownToExplosionC4, TimerFlags.REPEAT);
        Timers.Add(g_CountdownToExplosionC4);

        return HookResult.Continue;
    }

    public void CountdownToExplosionC4()
    {
        g_flTimerСountdownC4--;

        if (g_flTimerСountdownC4 == 0)
        {
            VirtualFunctions.ClientPrintAll(HudDestination.Center, $"C4 bomb exploded !!!", 0, 0, 0, 0);
        }
        else
        {
            string Style = "";

            if (g_flTimerСountdownC4 <= Config.TimerStarting)
            {
                if (Config.EnableProgressBar)
                {
                    int TimerStarting;

                    if (Config.TimerStarting > 0)
                    {
                        TimerStarting = Config.TimerStarting;
                    }
                    else
                    {
                        TimerStarting = (int)g_flTimerLengthC4;
                    }

                    for (int i = TimerStarting; i > 0; i--)
                    {
                        if (i > g_flTimerСountdownC4)
                        {
                            Style = Style + $"|";
                        }
                        else
                        {
                            Style = Style + $"-";
                        }
                    }

                    Style = $"[ {Style} ]";
                }

                if (Config.EnableTimer)
                {
                    if (string.IsNullOrEmpty(Style))
                    {
                        Style = $"{Config.LeftSideTimer}{g_flTimerСountdownC4}{Config.RightSideTimer}";
                    }
                    else
                    {
                        Style = ConnectTransferString($"{Config.LeftSideTimer}{g_flTimerСountdownC4}{Config.RightSideTimer}", Style);
                    }
                }
            }

            if (!string.IsNullOrEmpty(Style))
            {
                VirtualFunctions.ClientPrintAll(HudDestination.Center, Style, 0, 0, 0, 0);
            }
        }

        if (g_flTimerСountdownC4 == 0 || !g_bPlantedC4)
        {
            g_CountdownToExplosionC4!.Kill();
            Timers.Remove(g_CountdownToExplosionC4);
            g_CountdownToExplosionC4 = null;

            g_flTimerLengthC4 = float.NaN;
            g_flTimerСountdownC4 = float.NaN;
        }
    }

    public CPlantedC4? GetPlantedC4()
    {
        var PlantedC4 = Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4");

        if (PlantedC4 == null || !PlantedC4.Any())
        {
            return null;
        }

        return PlantedC4.FirstOrDefault();
    }

    string ConnectTransferString(string String1, string String2)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"{String1}\r\n{String2}";
        }
        else
        {
            return $"{String1}\n{String2}";
        }
    }
}
