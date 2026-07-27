using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Logoria
{
    public class Service
    {
        public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        public static ICommandManager CommandManager { get; private set; } = null!;
        public static IPluginLog Log { get; private set; } = null!;
        public static IGameGui GameGui { get; private set; } = null!;
        public static IFramework Framework { get; private set; } = null!;
        public static IChatGui ChatGui { get; private set; } = null!;
        public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
        public static IGameInteropProvider GameInterop { get; private set; } = null!;
        public static ITextureProvider TextureProvider { get; private set; } = null!;
        public static IDataManager DataManager { get; private set; } = null!;

        public static void Initialize(
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
            AddonLifecycle = addonLifecycle;
            GameInterop = gameInterop;
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            Log = log;
            GameGui = gameGui;
            Framework = framework;
            ChatGui = chatGui;
            TextureProvider = textureProvider;
            DataManager = dataManager;
        }
    }
}
