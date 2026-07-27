using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Logoria.Services;
using Logoria.UI;

namespace Logoria
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Logoria";

        public Configuration Configuration { get; }
        public WindowSystem WindowSystem { get; } = new("Logoria");

        public AssetService Assets { get; }
        public FontService Fonts { get; }
        public GameTextService GameText { get; }
        public JobAffinityService Jobs { get; }
        public MapService Maps { get; }
        public MnemeInventoryService Inventory { get; }
        public LogosDexService Dex { get; }
        public FarmingService Farming { get; }
        public ManipulatorService Manipulator { get; }
        public LogosLogReader LogReader { get; }

#if LOGORIA_DIAG
        public DiagnosticsService Diagnostics { get; }
        public EurekaStateProbe StateProbe { get; }
        public CallbackCaptureService CallbackCapture { get; }
#endif

        public MainWindow MainWindow { get; }
        public FloatingWindow FloatingWindow { get; }
        public SettingsWindow SettingsWindow { get; }
        public LogWindow LogWindow { get; }
        public FarmingWindow FarmingWindow { get; }
        public HelpWindow HelpWindow { get; }

#if LOGORIA_DIAG
        public DiagnosticsWindow DiagnosticsWindow { get; }
#endif

        private float scanTimer;
        private bool logWasOpen;

        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IPluginLog log,
            IGameGui gameGui,
            IFramework framework,
            IChatGui chatGui,
            IAddonLifecycle addonLifecycle,
            IGameInteropProvider gameInterop,
            ITextureProvider textureProvider,
            IDataManager dataManager)
        {
            Service.Initialize(pluginInterface, commandManager, log, gameGui, framework, chatGui,
                addonLifecycle, gameInterop, textureProvider, dataManager);

            Configuration = Service.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(Service.PluginInterface);
            Configuration.Migrate();

            // Brand the shared UI kit before any window draws.
            UI.Theme.Install(
                Configuration.ThemeName,
                Configuration.TextShadowStyle,
                Configuration.SurfaceGradient,
                Configuration.BevelStrength,
                Configuration.GlossStrength,
                Configuration.GlassMode,
                Configuration.GlassOpacity,
                Configuration.VanillaMode,
                Configuration.DomedTokens);

            Assets = new AssetService();
            Fonts = new FontService(Configuration.FontSizePx);
            UI.LogoriaWindow.Fonts = Fonts;

            GameText = new GameTextService();
            Jobs = new JobAffinityService();
            Maps = new MapService();
            Inventory = new MnemeInventoryService(Configuration);
            Dex = new LogosDexService(Configuration, Inventory);
            Farming = new FarmingService(Configuration, Inventory);
            Manipulator = new ManipulatorService(Configuration);
            LogReader = new LogosLogReader();

#if LOGORIA_DIAG
            Diagnostics = new DiagnosticsService();
            StateProbe = new EurekaStateProbe();
            CallbackCapture = new CallbackCaptureService();
#endif

            Dex.ActionDiscovered += OnActionDiscovered;

            MainWindow = new MainWindow(this);
            FloatingWindow = new FloatingWindow(this);
            SettingsWindow = new SettingsWindow(this);
            LogWindow = new LogWindow(this);
            FarmingWindow = new FarmingWindow(this);
            HelpWindow = new HelpWindow();

#if LOGORIA_DIAG
            DiagnosticsWindow = new DiagnosticsWindow(this);
#endif

            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(FloatingWindow);
            WindowSystem.AddWindow(SettingsWindow);
            WindowSystem.AddWindow(LogWindow);
            WindowSystem.AddWindow(FarmingWindow);
            WindowSystem.AddWindow(HelpWindow);

#if LOGORIA_DIAG
            WindowSystem.AddWindow(DiagnosticsWindow);
#endif

            Service.PluginInterface.UiBuilder.Draw += DrawUI;
            Service.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
            Service.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

            Service.Framework.Update += OnFrameworkUpdate;

            Service.CommandManager.AddHandler("/logoria", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the main Logoria Logos Dex window."
            });
            Service.CommandManager.AddHandler("/logofloat", new CommandInfo(OnFloatCommand)
            {
                HelpMessage = "Toggles the compact floating Logos tracker overlay."
            });
            Service.CommandManager.AddHandler("/logolog", new CommandInfo(OnLogCommand)
            {
                HelpMessage = "Opens the visual Logos collection log."
            });
            Service.CommandManager.AddHandler("/logofarm", new CommandInfo(OnFarmCommand)
            {
                HelpMessage = "Opens the farming planner."
            });
            Service.CommandManager.AddHandler("/logohelp", new CommandInfo(OnHelpCommand)
            {
                HelpMessage = "Opens the Logoria help and about pages."
            });
#if LOGORIA_DIAG
            Service.CommandManager.AddHandler("/logodiag", new CommandInfo(OnDiagCommand)
            {
                HelpMessage = "Opens the diagnostics window for wiring up the Logos Manipulator."
            });
#endif
        }

        public void Dispose()
        {
            Service.CommandManager.RemoveHandler("/logoria");
            Service.CommandManager.RemoveHandler("/logofloat");
            Service.CommandManager.RemoveHandler("/logolog");
            Service.CommandManager.RemoveHandler("/logofarm");
            Service.CommandManager.RemoveHandler("/logohelp");
#if LOGORIA_DIAG
            Service.CommandManager.RemoveHandler("/logodiag");
#endif

            // Unhook everything that can call back into us BEFORE tearing down what
            // those callbacks use. Disposing the font while Draw was still
            // subscribed left a window where a frame could push a dead handle.
            Service.Framework.Update -= OnFrameworkUpdate;
            Service.PluginInterface.UiBuilder.Draw -= DrawUI;
            Service.PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;
            Service.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;

            WindowSystem.RemoveAllWindows();

            Dex.ActionDiscovered -= OnActionDiscovered;

#if LOGORIA_DIAG
            Diagnostics.Dispose();
            CallbackCapture.Dispose();
#endif

            // Clear the kit's borrowed handles so nothing can outlive the plugin.
            UiKit.Ui.IconFont = null;
            UiKit.Ui.MonoFont = null;
            UiKit.Ui.ShadowTexture = null;
            UiKit.Ui.NoiseTexture = null;
            UiKit.Ui.NavHighlightTexture = null;
            UiKit.UiAnim.Clear();
            UiKit.Ui.ResetState();

            UI.LogoriaWindow.Fonts = null;
            Fonts.Dispose();
            MainWindow.Dispose();
            FloatingWindow.Dispose();
            SettingsWindow.Dispose();
            LogWindow.Dispose();
            FarmingWindow.Dispose();
            HelpWindow.Dispose();

#if LOGORIA_DIAG
            DiagnosticsWindow.Dispose();
#endif
        }

        /// <summary>
        /// Runs on the game's main thread. Mneme stock and the dex scan both read
        /// game memory, so they belong here rather than in Draw().
        /// </summary>
        private void OnFrameworkUpdate(IFramework framework)
        {
            // Every window that shows mneme counts has to keep the stock fresh.
            // Listing only the main and floating windows meant opening the farming
            // plan or collection log on their own showed stale or empty counts,
            // because nothing ever refreshed while they were the only thing visible.
            var needsStock = MainWindow.IsOpen
                             || FloatingWindow.IsOpen
                             || LogWindow.IsOpen
                             || FarmingWindow.IsOpen;

#if LOGORIA_DIAG
            needsStock |= DiagnosticsWindow.IsOpen;
#endif

            // Auto-fill runs here rather than in Draw: FireCallback drives the game's
            // UI and belongs on this thread. Drained every frame, not on the scan
            // interval, so pressing Fill still feels instant.
            Manipulator.ProcessPending();

            var interval = Math.Max(0.25f, Configuration.AutoDetectIntervalSeconds);

            scanTimer += (float)framework.UpdateDelta.TotalSeconds;
            if (scanTimer < interval) return;
            scanTimer = 0f;

            if (needsStock || Manipulator.IsManipulatorOpen())
                Inventory.Refresh();

            if (Configuration.EnableAutoDetect)
                Dex.Scan();

            if (Configuration.AutoOpenOnManipulator && Manipulator.JustOpened())
                MainWindow.IsOpen = true;

            SyncFromLogIfOpen();
        }

        /// <summary>
        /// Drake's Logos Action Log is the authoritative record, so sync from it as
        /// soon as it appears. Runs once per opening rather than every scan tick.
        /// </summary>
        private void SyncFromLogIfOpen()
        {
            if (!Configuration.AutoSyncFromLog) return;

            var open = LogReader.IsLogOpen();
            if (!open) { logWasOpen = false; return; }
            if (logWasOpen) return;

            logWasOpen = true;

            var (read, added, registered) = LogReader.TrySyncBest(Configuration);
            if (!read) return;

            Service.Log.Information($"Logoria: synced from the Logos Action Log ({registered}/56, {added} new).");

            if (!Configuration.AnnounceDiscoveriesInChat) return;

            Service.ChatGui.Print(added > 0
                ? $"[Logoria] Synced your Logos Action Log: {registered}/56 registered, {added} newly recorded."
                : $"[Logoria] Logos Action Log synced: {registered}/56 registered, already up to date.");
        }

        private void OnActionDiscovered(Data.LogosAction action)
        {
            if (!Configuration.AnnounceDiscoveriesInChat) return;
            Service.ChatGui.Print($"[Logoria] New Logos Action recorded: {GameText.ActionName(action)}");
        }

        private void OnCommand(string command, string args) => ToggleMainUI();

        private void OnFloatCommand(string command, string args) =>
            FloatingWindow.IsOpen = !FloatingWindow.IsOpen;

        private void OnLogCommand(string command, string args) =>
            LogWindow.IsOpen = !LogWindow.IsOpen;

        private void OnFarmCommand(string command, string args) =>
            FarmingWindow.IsOpen = !FarmingWindow.IsOpen;

        private void OnHelpCommand(string command, string args) =>
            HelpWindow.IsOpen = !HelpWindow.IsOpen;

#if LOGORIA_DIAG
        /// <summary>
        /// Typing the command counts as asking for it, so it turns the window on
        /// rather than doing nothing when diagnostics are hidden. Nobody types
        /// /logodiag by accident, and a command that silently no-ops is the worst
        /// possible answer when someone is already troubleshooting.
        /// </summary>
        private void OnDiagCommand(string command, string args)
        {
            if (!Configuration.ShowDiagnostics)
            {
                Configuration.ShowDiagnostics = true;
                Configuration.Save();
                Service.ChatGui.Print(
                    "[Logoria] Diagnostics enabled. Turn it back off under Settings, Troubleshooting.");
            }

            DiagnosticsWindow.IsOpen = !DiagnosticsWindow.IsOpen;
        }
#endif

        private void DrawUI()
        {
            // Hand the kit our art each frame. Textures load asynchronously, so the
            // handles only become valid once ready; until then the kit falls back to
            // its procedural drawing on its own.
            UiKit.Ui.ShadowTexture = Assets.Shadow?.Handle;

            // The nav selection is drawn procedurally on purpose. The sprite version
            // baked its own fill, glow and rim at a fixed resolution, so it softened
            // at row height and could not follow the theme's rounding; the gradient
            // plus bevel plus gloss path is sharper and tracks the palette exactly.
            // The kit still supports a sprite for other plugins.
            UiKit.Ui.NavHighlightTexture = null;

            // FontAwesome, supplied per frame because the handle is only valid while
            // the font is built. The kit falls back to no icon when this is unset.
            UiKit.Ui.IconFont = Service.PluginInterface.UiBuilder.FontIcon;
            UiKit.Ui.MonoFont = Service.PluginInterface.UiBuilder.FontMono;

            UiKit.UiAnim.Enabled = Configuration.EnableAnimation;
            UiKit.UiAnim.SpeedScale = Configuration.AnimationSpeed;

            var noise = Configuration.NoiseStrength > 0f ? Assets.Noise : null;
            UiKit.Ui.NoiseTexture = noise?.Handle;
            UiKit.Ui.NoiseStrength = Configuration.NoiseStrength;

            WindowSystem.Draw();
        }

        public void ToggleMainUI() => MainWindow.IsOpen = !MainWindow.IsOpen;

        public void ToggleConfigUI() => SettingsWindow.IsOpen = !SettingsWindow.IsOpen;
    }
}
