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

    [JsonPropertyName("EnableProgressBar")]
    public bool EnableProgressBar { get; set; } = true;

    [JsonPropertyName("TimerStarting")]
    public int TimerStarting { get; set; } = 45;

    [JsonPropertyName("LeftSideTimer")]
    public string LeftSideTimer { get; set; } = "-[ ";

    [JsonPropertyName("RightSideTimer")]
    public string RightSideTimer { get; set; } = " ]-";

    [JsonPropertyName("EnableColorMessage")]
    public bool EnableColorMessage { get; set; } = true;

    [JsonPropertyName("SidesTimerColor")]
    public string SidesTimerColor { get; set; } = "45:white";

    [JsonPropertyName("TimeColor")]
    public string TimeColor { get; set; } = "20:yellow, 10:red, 5:darkred";

    [JsonPropertyName("ProgressBarColor")]
    public string ProgressBarColor { get; set; } = "20:yellow, 10:red, 5:darkred";
}

public class C4Timer : BasePlugin, IPluginConfig<C4TimerConfig>
{
    public override string ModuleName => "C4 Timer";
    public override string ModuleVersion => "1.4";
    public override string ModuleAuthor => "belom0r";

    Dictionary<int, string> TimeColor = new Dictionary<int, string>();
    Dictionary<int, string> ProgressBarColor = new Dictionary<int, string>();
    Dictionary<int, string> SidesTimerColor = new Dictionary<int, string>();

    private bool g_bPlantedC4 = false;
    private float g_flTimerLengthC4 = float.NaN;
    private float g_flTimerСountdownC4 = float.NaN;

    private string g_MsgTimerСountdownC4 = "";

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

        if (Config.EnableColorMessage)
        {
            RegisterListener<Listeners.OnTick>(OnTick);
        }

        if (Config.EnableColorMessage)
        {
            ColorMsg(Config.TimeColor, TimeColor);
            ColorMsg(Config.ProgressBarColor, ProgressBarColor);
            ColorMsg(Config.SidesTimerColor, SidesTimerColor);
        }

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

    public void OnTick()
    {
        if (!string.IsNullOrEmpty(g_MsgTimerСountdownC4))
        {
            List<CCSPlayerController> Players = GetPlayers();

            foreach (var Player in Players)
            {
                Player.PrintToCenterHtml(g_MsgTimerСountdownC4);
            }
        }
    }

    public void CountdownToExplosionC4()
    {
        g_flTimerСountdownC4--;

        string Style = "";

        if (g_flTimerСountdownC4 == 0)
        {
            if (Config.EnableColorMessage)
            {
                Style = "<font class='fontSize-m' color='darkred'>C4 bomb exploded !!!</font>";
            }
            else
            {
                Style = "C4 bomb exploded !!!";
            }
        }
        else
        {
            string StyleTimer = "";
            string StyleProgressBar = "";

            if (g_flTimerСountdownC4 <= Config.TimerStarting)
            {
                if (Config.EnableProgressBar)
                {
                    int TimerStarting;

                    if (Config.TimerStarting > 0)
                        TimerStarting = Config.TimerStarting;
                    else
                        TimerStarting = (int)g_flTimerLengthC4;

                    for (int i = TimerStarting; i > 0; i--)
                    {
                        if (i > g_flTimerСountdownC4)
                            StyleProgressBar = StyleProgressBar + $"|";
                        else
                            StyleProgressBar = StyleProgressBar + $"-";
                    }

                    StyleProgressBar = $"[ {StyleProgressBar} ]";
                }

                if (Config.EnableTimer)
                {
                    if (Config.EnableColorMessage)
                        StyleTimer = 
                            $"<font class='fontSize-m' color='{SidesTimerColor[(int)g_flTimerСountdownC4]}'>{Config.LeftSideTimer}</font>" +
                            $"<font class='fontSize-m' color='{TimeColor[(int)g_flTimerСountdownC4]}'>{g_flTimerСountdownC4}</font>" +
                            $"<font class='fontSize-m' color='{SidesTimerColor[(int)g_flTimerСountdownC4]}'>{Config.RightSideTimer}</font>";
                    else
                        StyleTimer = $"{Config.LeftSideTimer}{g_flTimerСountdownC4}{Config.RightSideTimer}";
                }

                if (Config.EnableColorMessage)
                    Style = $"{StyleTimer}<br><font class='fontSize-m' color='{ProgressBarColor[(int)g_flTimerСountdownC4]}'>{StyleProgressBar}</font>";
                else
                    Style = ConnectTransferString(StyleTimer, StyleProgressBar);
            }
        }

        if (!string.IsNullOrEmpty(Style))
        {
            if (Config.EnableColorMessage)
                g_MsgTimerСountdownC4 = Style;
            else
                VirtualFunctions.ClientPrintAll(HudDestination.Center, Style, 0, 0, 0, 0);
        }

        if (g_flTimerСountdownC4 == 0 || !g_bPlantedC4)
        {
            g_CountdownToExplosionC4!.Kill();
            Timers.Remove(g_CountdownToExplosionC4);
            g_CountdownToExplosionC4 = null;

            g_flTimerLengthC4 = float.NaN;
            g_flTimerСountdownC4 = float.NaN;
            g_MsgTimerСountdownC4 = "";
        }
    }

    public CPlantedC4? GetPlantedC4()
    {
        var PlantedC4 = Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4");

        if (PlantedC4 == null || !PlantedC4.Any())
            return null;

        return PlantedC4.FirstOrDefault();
    }

    public string ConnectTransferString(string String1, string String2)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"{String1}\r\n{String2}";
        else
            return $"{String1}\n{String2}";
    }

    public void ColorMsg(string Msg, Dictionary<int, string> TimeColor)
    {
        TimeColor.Clear();

        for (int i = 0; i <= Config.TimerStarting; i++)
            TimeColor.Add(i, "white");

        if (!string.IsNullOrEmpty(Msg))
        {
            string[] Colors = Msg.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var Color in Colors)
            {
                string[] Elements = Color.Split(':', StringSplitOptions.RemoveEmptyEntries);

                for (int i = int.Parse(Elements[0]); i >= 0; i--)
                    TimeColor[i] = Elements[1];
            }
        }
    }

    public List<CCSPlayerController> GetPlayers()
    {
        List<CCSPlayerController> players = Utilities.GetPlayers();
        return players.FindAll(player => player != null && player.IsValid && player.PlayerPawn.IsValid && player.PlayerPawn.Value?.IsValid == true && player.Connected == PlayerConnectedState.PlayerConnected);
    }
}