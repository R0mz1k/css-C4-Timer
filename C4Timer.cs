using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
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

    [JsonPropertyName("LeftSidesTimerColor")]
    public string LeftSidesTimerColor { get; set; } = "45:white";

    [JsonPropertyName("RightSidesTimerColor")]
    public string RightSidesTimerColor { get; set; } = "45:white";

    [JsonPropertyName("TimeColor")]
    public string TimeColor { get; set; } = "20:yellow, 10:red, 5:darkred";

    [JsonPropertyName("ProgressBarColor")]
    public string ProgressBarColor { get; set; } = "20:yellow, 10:red, 5:darkred";

    [JsonPropertyName("ExplodedMessage")]
    public string ExplodedMessage { get; set; } = "C4 bomb exploded !!!";

    [JsonPropertyName("ExplodedMessageColor")]
    public string ExplodedMessageColor { get; set; } = "darkred";

    [JsonPropertyName("DefusedMessage")]
    public string DefusedMessage { get; set; } = "C4 bomb defused !!!";

    [JsonPropertyName("DefusedMessageColor")]
    public string DefusedMessageColor { get; set; } = "yellow";
}

public class C4Timer : BasePlugin, IPluginConfig<C4TimerConfig>
{
    public override string ModuleName => "C4 Timer";
    public override string ModuleVersion => "1.7";
    public override string ModuleAuthor => "belom0r";

    Dictionary<int, string> TimeColor = new Dictionary<int, string>();
    Dictionary<int, string> ProgressBarColor = new Dictionary<int, string>();
    Dictionary<int, string> LeftSidesTimerColor = new Dictionary<int, string>();
    Dictionary<int, string> RightSidesTimerColor = new Dictionary<int, string>();

    private int TimerLength = 0;
    private int TimerCountdown = 0;
    private int TimerStarting = 0;

    private string messageCountdown = "";

    private Timer? CountdownToExplosion = null;

    public required C4TimerConfig Config { get; set; }

    public void OnConfigParsed(C4TimerConfig config) { Config = config; }

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventRoundPrestart>((_, _) => 
        {
            ResetCountdown();
            return HookResult.Continue; 
        });

        RegisterEventHandler<EventBombPlanted>(BombPlantedPost);
        
        RegisterEventHandler<EventBombExploded>((_, _) => 
        {
            ResetCountdown();
            messageCountdown = WrapWithColor(Config.ExplodedMessage, Config.ExplodedMessageColor);
            AddTimer(3.0f, () => messageCountdown = "", TimerFlags.STOP_ON_MAPCHANGE);
            return HookResult.Continue; 
        });

        RegisterEventHandler<EventBombDefused>((_, _) => 
        {
            ResetCountdown();
            messageCountdown = WrapWithColor(Config.DefusedMessage, Config.DefusedMessageColor);
            AddTimer(3.0f, () => messageCountdown = "", TimerFlags.STOP_ON_MAPCHANGE);
            return HookResult.Continue; 
        });

        RegisterListener<Listeners.OnTick>(OnTick);
    }

    private HookResult BombPlantedPost(EventBombPlanted @event, GameEventInfo info)
    {
        ResetCountdown();

        var planted = GetPlantedC4();

        if (planted == null)
            return HookResult.Continue;

        TimerLength = TimerCountdown = (int)(planted.TimerLength + 1.0f);

        TimerStarting = Math.Clamp(Config.TimerStarting, 0, TimerLength);

        ColorMsg(Config.TimeColor, TimeColor);
        ColorMsg(Config.ProgressBarColor, ProgressBarColor);
        ColorMsg(Config.LeftSidesTimerColor, LeftSidesTimerColor);
        ColorMsg(Config.RightSidesTimerColor, RightSidesTimerColor);

        CountdownToExplosion = AddTimer(1.0f, CountdownToExplosionC4, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);

        return HookResult.Continue;
    }

    public void OnTick()
    {
        if (string.IsNullOrEmpty(messageCountdown))
            return;

        var players = Utilities.GetPlayers();

        foreach (var player in players)
        {
            if (player == null || !player.IsValid)
            {
                continue;
            }

            player.PrintToCenterHtml(messageCountdown);
        }
    }

    public void CountdownToExplosionC4()
    {
        TimerCountdown--;

        if (TimerCountdown <= 0)
        {
            ResetCountdown();
            return;
        }

        messageCountdown = GenerateCountdownMessage();

    }

    private void ResetCountdown()
    {
        CountdownToExplosion?.Kill();
        CountdownToExplosion = null;

        TimerLength = 0;
        TimerCountdown = 0;
        messageCountdown = "";
    }

    private string GenerateCountdownMessage()
    {
        if (TimerCountdown > TimerStarting)
            return "";

        string timerStyle = GenerateTimerStyle();
        string progressBarStyle = GenerateProgressBarStyle();

        return $"{timerStyle}{progressBarStyle}";
    }

    private string GenerateTimerStyle()
    {
        if (!Config.EnableTimer)
            return "";

        string leftSide = WrapWithColor(Config.LeftSideTimer, LeftSidesTimerColor[TimerCountdown]);
        string time = WrapWithColor(TimerCountdown.ToString(), TimeColor[TimerCountdown]);
        string rightSide = WrapWithColor(Config.RightSideTimer, RightSidesTimerColor[TimerCountdown]);

        return $"{leftSide}{time}{rightSide}";
    }

    private string GenerateProgressBarStyle()
    {
        if (!Config.EnableProgressBar)
            return "";

        int total = Math.Min(TimerStarting, TimerLength);

        char[] progressBar = new char[total];
        for (int i = 0; i < total; i++)
            progressBar[i] = i >= TimerCountdown ? '-' : '|';

        string progressBar_txt = WrapWithColor(new string(progressBar), ProgressBarColor[TimerCountdown]);

        return Config.EnableTimer ? "<br>" + progressBar_txt : progressBar_txt;
    }

    private string WrapWithColor(string text, string color)
    {
        return Config.EnableColorMessage ? $"<font color='{color}'>{text}</font>" : text;
    }

    private CPlantedC4? GetPlantedC4()
    {
        var planted_c4 = Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4");

        if (planted_c4 == null || !planted_c4.Any())
            return null;

        return planted_c4.FirstOrDefault();
    }

    public void ColorMsg(string msg, Dictionary<int, string> colorDictionary)
    {
        colorDictionary.Clear();

        for (int i = 0; i <= TimerStarting; i++)
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
}
