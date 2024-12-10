using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
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
    public override string ModuleVersion => "1.6";
    public override string ModuleAuthor => "belom0r";

    Dictionary<int, string> TimeColor = new Dictionary<int, string>();
    Dictionary<int, string> ProgressBarColor = new Dictionary<int, string>();
    Dictionary<int, string> SidesTimerColor = new Dictionary<int, string>();

    private bool PlantedC4 = false;

    private int TimerLength = 0;
    private int TimerСountdown = 0;

    private string messageCountdown = "";

    private Timer? CountdownToExplosion;

    public required C4TimerConfig Config { get; set; }

    public void OnConfigParsed(C4TimerConfig config) { Config = config; }

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventBombPlanted>(BombPlantedPost); //bPlantedC4 = true
        RegisterEventHandler<EventRoundPrestart>((_, _) => { PlantedC4 = false; return HookResult.Continue; });
        RegisterEventHandler<EventBombExploded>((_, _) => { PlantedC4 = false; return HookResult.Continue; });
        RegisterEventHandler<EventBombDefused>((_, _) => { PlantedC4 = false; return HookResult.Continue; });

        if (Config.EnableColorMessage)
        {
            RegisterListener<Listeners.OnTick>(OnTick);
        }

        ColorMsg(Config.TimeColor, TimeColor);
        ColorMsg(Config.ProgressBarColor, ProgressBarColor);
        ColorMsg(Config.SidesTimerColor, SidesTimerColor);
    }

    private HookResult BombPlantedPost(EventBombPlanted @event, GameEventInfo info)
    {
        var planted = GetPlantedC4();

        if (planted == null)
            return HookResult.Continue;

        PlantedC4 = true;

        TimerLength = TimerСountdown = (int)(planted.TimerLength + 1.0f);

        Config.TimerStarting = Math.Clamp(Config.TimerStarting, 0, TimerLength);

        CountdownToExplosion = new Timer(1.0f, CountdownToExplosionC4, TimerFlags.REPEAT);

        Timers.Add(CountdownToExplosion);

        return HookResult.Continue;
    }

    public void OnTick()
    {
        if (string.IsNullOrEmpty(messageCountdown))
            return;

        foreach (var Player in GetPlayers())
        {
            Player.PrintToCenterHtml(messageCountdown);
        }
    }

    public void CountdownToExplosionC4()
    {
        TimerСountdown--;

        if ((int)TimerСountdown == 0)
        {
            messageCountdown = WrapWithColor("C4 bomb exploded !!!", "darkred");
        }
        else if (TimerСountdown < 0 || !PlantedC4)
        {
            CountdownToExplosion!.Kill();
            Timers.Remove(CountdownToExplosion);
            CountdownToExplosion = null;

            TimerLength = 0;
            TimerСountdown = 0;

            messageCountdown = "";

            return;
        }
        else messageCountdown = GenerateCountdownMessage();

        if (!Config.EnableColorMessage)
            VirtualFunctions.ClientPrintAll(HudDestination.Center, messageCountdown, 0, 0, 0, 0);
    }

    private string GenerateCountdownMessage()
    {
        if (TimerСountdown > Config.TimerStarting)
            return "";

        string timerStyle = GenerateTimerStyle();
        string progressBarStyle = GenerateProgressBarStyle();

        return Config.EnableColorMessage
            ? $"{timerStyle}{progressBarStyle}"
            : ConnectStrings(timerStyle, progressBarStyle);
    }

    private string GenerateTimerStyle()
    {
        if (!Config.EnableTimer)
            return "";

        string leftSide = WrapWithColor(Config.LeftSideTimer, SidesTimerColor[TimerСountdown]);
        string time = WrapWithColor(TimerСountdown.ToString(), TimeColor[TimerСountdown]);
        string rightSide = WrapWithColor(Config.RightSideTimer, SidesTimerColor[TimerСountdown]);

        return $"{leftSide}{time}{rightSide}";
    }

    private string GenerateProgressBarStyle()
    {
        if (!Config.EnableProgressBar)
            return "";

        int total = Math.Min(Config.TimerStarting, TimerLength);

        char[] progressBar = new char[total];
        for (int i = 0; i < total; i++)
            progressBar[i] = i >= TimerСountdown ? '-' : '|';

        string progressBar_txt = WrapWithColor(new string(progressBar), ProgressBarColor[TimerСountdown]);

        return Config.EnableTimer && Config.EnableColorMessage ? "<br>" + progressBar_txt : progressBar_txt;
    }

    private string WrapWithColor(string text, string color)
    {
        return Config.EnableColorMessage ? $"<font color='{color}'>{text}</font>" : text;
    }

    private CPlantedC4? GetPlantedC4()
    {
        var PlantedC4 = Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4");

        if (PlantedC4 == null || !PlantedC4.Any())
            return null;

        return PlantedC4.FirstOrDefault();
    }

    public string ConnectStrings(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str2))
            return str1;

        return $"{str1}{Environment.NewLine}{str2}";
    }

    public void ColorMsg(string msg, Dictionary<int, string> colorDictionary)
    {
        colorDictionary.Clear();

        for (int i = 0; i <= Config.TimerStarting; i++)
            colorDictionary[i] = "white";

        if (!Config.EnableColorMessage || string.IsNullOrEmpty(msg))
            return;

        foreach (var color in msg.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var elements = color.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (elements.Length != 2) continue;

                int index = int.Parse(elements[0]);
                string colorValue = elements[1];

                for (int i = index; i >= 0; i--)
                    colorDictionary[i] = colorValue;
            }
            catch
            {
                Logger.LogError($"Invalid color format: {color}");
            }
        }
    }

    public List<CCSPlayerController> GetPlayers()
    {
        return Utilities.GetPlayers().Where(player =>
            player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected).ToList();
    }
}