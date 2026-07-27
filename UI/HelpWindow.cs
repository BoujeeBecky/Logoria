using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Logoria.UI
{
    /// <summary>
    /// How-to and about, as a two pane reader: section list on the left, prose on
    /// the right.
    /// <para>
    /// Deliberately plain. Everything here is text a player reads once, so it uses
    /// wrapped body copy rather than the shell's decorated components: a help page
    /// that needs its own explaining has failed. It is also the one page that must
    /// stay legible in vanilla mode, since that is the mode someone picks when the
    /// rest of the UI is fighting their machine.
    /// </para>
    /// </summary>
    public class HelpWindow : LogoriaWindow, IDisposable
    {
        private const float SectionListWidth = 168f;

        private static readonly string[] Sections =
        {
            "Getting Started",
            "The Dex",
            "Collection Log",
            "Farming Plan",
            "Map Pins",
            "Floating Tracker",
            "Logos Manipulator",
            "Commands",
            "Appearance",
            "Troubleshooting",
            "About",
        };

        private int section;

        // No Plugin reference: every word on this page is static prose, so holding
        // one would only be a field that looks like it means something.
        public HelpWindow()
            : base("Logoria - Help###LogoriaHelpWindow")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(620, 420),
                MaximumSize = new Vector2(1600, 1400),
            };
            Size = new Vector2(820, 580);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Dispose() { }

        public override void Draw() => DrawContent();

        /// <summary>Body only, so the main shell can host this as a page.</summary>
        public void DrawContent()
        {
            ImGui.BeginChild("HelpSections", new Vector2(SectionListWidth, 0));

            // Exactly one section is ever selected, so this is the ideal case for the
            // sliding highlight.
            Theme.BeginNavGroup("HelpSections");

            for (var i = 0; i < Sections.Length; i++)
            {
                if (Theme.NavItem(Sections[i], section == i))
                    section = i;
            }

            Theme.EndNavGroup();
            ImGui.EndChild();

            ImGui.SameLine();

            // Border true purely for the padding: ImGui only pads bordered children,
            // and without it the prose butts against the section list.
            ImGui.BeginChild("HelpBody", new Vector2(0, 0), true);

            switch (section)
            {
                case 1: DrawDex(); break;
                case 2: DrawCollectionLog(); break;
                case 3: DrawFarming(); break;
                case 4: DrawMapPins(); break;
                case 5: DrawFloating(); break;
                case 6: DrawManipulator(); break;
                case 7: DrawCommands(); break;
                case 8: DrawAppearance(); break;
                case 9: DrawTroubleshooting(); break;
                case 10: DrawAbout(); break;
                default: DrawGettingStarted(); break;
            }

            ImGui.EndChild();
        }

        // ---- section bodies -------------------------------------------------

        private void DrawGettingStarted()
        {
            Header("Getting Started");
            Body("Logoria is a collection log for the 56 Logos Actions in the Forbidden Land, "
                 + "Eureka. It tracks which ones you have registered, which ones you could "
                 + "synthesise right now, and what you still need to farm.");

            Gap();
            Header("The one thing worth doing first");
            Body("Speak to Drake, standing beside the Logos Manipulator, and open your Logos "
                 + "Action Log. Logoria reads it the moment it opens and fills in your whole dex "
                 + "at once.");
            Note("The game itself keeps that record. Nothing is guessed, and no synthesis or "
                 + "materials are needed to import it.");

            Gap();
            Header("After that");
            Bullet("Your dex updates on its own. Slotting a Logos Action records it permanently, "
                   + "even after you unslot it.");
            Bullet("Stand at the Logos Manipulator and your mneme stock is read live, so the dex "
                   + "can tell you what you can make right now.");
            Bullet("Everything is per character. A dex that looks empty is a different character's, "
                   + "not lost data, and Logoria says so on the dex page when it notices.");
        }

        private void DrawDex()
        {
            Header("The Dex");
            Body("One row per Logos Action, with its icon, the jobs that can use it, its effect, "
                 + "and a recipe.");

            Gap();
            Header("The three states");
            StateLine("Obtained", Theme.Success,
                "Registered. You have made this one before.");
            StateLine("READY", Theme.Accent,
                "You hold the mnemes for a combination but have never registered it. These rows "
                + "are tinted, because this is the whole point of the dex.");
            StateLine("Unknown", Theme.TextFaint,
                "Not registered, and you are short of at least one mneme for every combination.");

            Gap();
            Body("Click a status dot to set or clear Obtained by hand, for the rare case where "
                 + "the automatic sources disagree with reality.");

            Gap();
            Header("Recipes");
            Body("The recipe column shows the combination you can actually make. If you cannot "
                 + "make any of them, it shows the cheapest instead, so you know what to farm "
                 + "toward. Counts read have / needed, and turn green when you have enough.");
            Note("Fewer mnemes in a combination means a higher success rate. Where an action has "
                 + "several combinations, hover the recipe to see all of them.");

            Gap();
            Header("Filters");
            Body("Search matches names and effect text. The radios narrow to one state, and "
                 + "\"Only what I can make now\" hides everything you are short of.");
        }

        private void DrawCollectionLog()
        {
            Header("Collection Log");
            Body("The same 56 actions as a grid of icons rather than a table, in the game's own "
                 + "log order. It is the fastest way to see how far along you are.");

            Gap();
            Bullet("Registered entries are lit; the rest are dimmed.");
            Bullet("Hover any entry for its name, jobs, effect and recipe.");
            Bullet("Click an entry to toggle whether it is registered.");

            Gap();
            Note("Log numbers here match Drake's log, so you can compare the two side by side.");
        }

        private void DrawFarming()
        {
            Header("Farming Plan");
            Body("Press Farm on any dex row to add that action to your farm list. The plan totals "
                 + "the mnemes across everything on the list and groups them by the logogram that "
                 + "yields them, so one trip covers several actions.");

            Gap();
            Header("Reading the shopping list");
            Bullet("Each group header is a logogram, with how many of its mnemes you are still short.");
            Bullet("Under it, the mnemes themselves, with have / needed counts.");
            Bullet("Under that, how the logogram drops and which enemies drop it.");

            Gap();
            Header("Which combination it plans for");
            Body("When an action has several combinations, the plan picks the one you are closest "
                 + "to finishing, not the smallest one. Ranking by shortfall means the list stops "
                 + "reshuffling itself every time you pick up a mneme.");

            Gap();
            Note("\"Add everything I can almost make\" is a quick start: it adds every unregistered "
                 + "action that is a single mneme away.");
        }

        private void DrawMapPins()
        {
            Header("Map Pins");
            Body("Each logogram you still need lists the places it drops. Press Map on a row to "
                 + "open that zone with a marker on the spot.");

            Gap();
            Header("How accurate is this?");
            Body("Coordinates are community-sourced from the FFXIV wikis. They should be accurate "
                 + "rather than are accurate, and Logoria says so above the list rather than "
                 + "quietly presenting them as fact.");

            Gap();
            Body("Two honest caveats are built into the display:");
            Bullet("Entries marked ~ are approximate. Hover the mark and it tells you why that "
                   + "particular one is uncertain, usually because the wiki has no location table "
                   + "and what is pinned is the triggering FATE instead.");
            Bullet("Several of these enemies spawn in more than one place, which is why a logogram "
                   + "can list several rows. Take the nearest.");

            Gap();
            Header("Sprites and adaptation");
            Body("Eureka's adaptation mechanic levels a sprite up in place. There is no separate "
                 + "higher-level sprite somewhere else on the map: it is the same spawn under "
                 + "different weather. Go to the listed spot and wait for the weather.");
        }

        private void DrawFloating()
        {
            Header("Floating Tracker");
            Body("A small pinnable overlay showing what you are working toward, meant to sit on "
                 + "screen while you farm so you do not have to keep opening the main window.");

            Gap();
            Bullet("Adding anything to your farm list opens it automatically.");
            Bullet("Click an entry to toggle whether it is registered.");
            Bullet("Hover an entry for its recipe and what you are short of.");

            Gap();
            Body("Settings can lock it in place, hide entries you already own, and set how "
                 + "transparent it is. Locking removes the title bar as well as the ability to "
                 + "drag it, so lock it once it is where you want it.");
        }

        private void DrawManipulator()
        {
            Header("Logos Manipulator");
            Body("Stand at the manipulator and Logoria reads your mneme stock live from the "
                 + "window. The pill on the dex page tells you whether the stock it is showing is "
                 + "live or remembered from last time.");

            Gap();
            Header("The Fill button");
            Body("Fill clears the Astral Array and loads a combination into it, so you do not have "
                 + "to place each mneme by hand. It is only enabled while the manipulator is open "
                 + "and you hold the mnemes for at least one combination.");

            Gap();
            Warn("Fill deliberately stops before synthesising.");
            Body("It never presses Extract Mneme for you. That last click is yours, which means "
                 + "Logoria cannot consume your materials by accident or by bug.");

            Gap();
            Note("Settings can open Logoria automatically whenever the manipulator opens.");
        }

        private void DrawCommands()
        {
            Header("Commands");
            Command("/logoria", "The main window: dex, collection log, farming plan and settings.");
            Command("/logofloat", "Toggles the floating tracker overlay.");
            Command("/logolog", "Opens the visual collection log on its own.");
            Command("/logofarm", "Opens the farming plan on its own.");
            Command("/logohelp", "This page.");
#if LOGORIA_DIAG
            Command("/logodiag", "Diagnostics. Development builds only.");
#endif

            Gap();
            Note("Every page in the main window is also available as its own window, so you can "
                 + "pull just the farming plan out and leave the rest closed.");
        }

        private void DrawAppearance()
        {
            Header("Appearance");
            Body("Settings has ten themes and a set of controls over how heavy the styling is. "
                 + "Everything applies live, so you can watch it change as you drag.");

            Gap();
            Bullet("Themes swap the whole palette. Semantic colours stay put: green still means "
                   + "you have enough and red still means you do not, in every theme.");
            Bullet("Gradient, bevel, gloss and film grain each dial back to zero independently.");
            Bullet("Glass mode makes panels translucent. Text switches to an outline shadow "
                   + "automatically, because a drop shadow is not enough once the game shows through.");
            Bullet("Animation can be turned off, or slowed down and sped up.");

            Gap();
            Header("Vanilla mode");
            Body("Vanilla strips all of it out and draws plain ImGui: no gradients, no shadows, no "
                 + "custom panels. It exists for anyone who would rather spend the frames on the "
                 + "game. It is off by default, and it is a single checkbox in Settings.");
        }

        private void DrawTroubleshooting()
        {
            Header("Troubleshooting");

            Sub("My dex is empty");
            Body("It is per character. If you have recorded actions on another character, the dex "
                 + "page says so rather than leaving you guessing. Otherwise, open Drake's Logos "
                 + "Action Log to import everything at once.");

            Gap();
            Sub("Mneme counts look wrong or say zero");
            Body("Stock can only be read from the Logos Manipulator's own window, so it is live "
                 + "while you are standing there and remembered otherwise. The pill on the dex "
                 + "page tells you which you are looking at.");

            Gap();
            Sub("The Fill button is greyed out");
            Body("It needs the manipulator open and enough mnemes for at least one combination.");

            Gap();
            Sub("Something broke after a game patch");
            Body("Report it. The tooling that identifies a renamed window or a moved data array "
                 + "lives in the development build only, so a fix comes as a plugin update rather "
                 + "than something you switch on.");
        }

        private void DrawAbout()
        {
            Header("About Logoria");

            var version = typeof(Plugin).Assembly.GetName().Version;
            Body($"Version {version?.ToString(3) ?? "unknown"}, by Boujee Becky.");

            Gap();
            Header("Where the data comes from");
            Bullet("Action names, icons, descriptions and job restrictions are read from the "
                   + "game's own files, so they stay correct across patches.");
            Bullet("Your registered actions come from the game: Drake's Logos Action Log, and what "
                   + "you have slotted.");
            Bullet("Recipes were derived from the public tables at ffxiv-eureka.com.");
            Bullet("Farming locations are community-sourced from the FFXIV wikis, and are marked "
                   + "with a confidence rather than presented as fact.");

            Gap();
            Header("What it will not do");
            Bullet("No network access of any kind. Nothing is uploaded, and there is nothing to "
                   + "upload it with.");
            Bullet("Never sends chat, never moves your character, never presses a game button "
                   + "you did not press.");
            Bullet("Never synthesises and never consumes a material. Auto-fill loads the Astral "
                   + "Array and stops; every irreversible click stays yours.");
            Bullet("Watches only the four Eureka windows it needs, and only while you have one "
                   + "open.");

            Gap();
            Note("The interface is built on a shared UI kit written alongside this plugin, which is "
                 + "why the themes, panels and animation are consistent throughout.");
        }

        // ---- small typographic helpers --------------------------------------

        private static void Header(string text)
        {
            Theme.TextColored(Theme.Gold, text);
            ImGui.Separator();
            ImGui.Spacing();
        }

        private static void Sub(string text) => Theme.TextColored(Theme.Accent, text);

        private static void Body(string text) => ImGui.TextWrapped(text);

        private static void Bullet(string text)
        {
            // The gap was 2px, which put the dot hard against the first word. ImGui's
            // own list spacing is the right reference here rather than a guess.
            // TextWrapped starts every wrapped line at the cursor, so the hanging
            // indent under the first word comes for free.
            ImGui.Bullet();
            ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X * 2f);
            ImGui.TextWrapped(text);
            ImGui.Spacing();
        }

        private static void Gap()
        {
            ImGui.Spacing();
            ImGui.Spacing();
        }

        /// <summary>An aside: true, but not the main point of the section.</summary>
        private static void Note(string text)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.U32(Theme.TextFaint));
            ImGui.TextWrapped(text);
            ImGui.PopStyleColor();
        }

        /// <summary>Something the reader would regret not having read.</summary>
        private static void Warn(string text)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.U32(Theme.Gold));
            ImGui.TextWrapped(text);
            ImGui.PopStyleColor();
        }

        private static void Command(string command, string description)
        {
            Theme.TextMono(command, Theme.Accent);
            ImGui.Indent(16f);
            ImGui.TextWrapped(description);
            ImGui.Unindent(16f);
            ImGui.Spacing();
        }

        private static void StateLine(string label, Vector4 colour, string description)
        {
            Theme.StatusDot(label, colour, filled: true);
            ImGui.Indent(16f);
            ImGui.TextWrapped(description);
            ImGui.Unindent(16f);
            ImGui.Spacing();
        }
    }
}
