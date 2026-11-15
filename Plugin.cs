using System;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace BgReconnect
{
    internal static class PluginInfo
    {
        // GUID == AssemblyName: Firestone lists a mod by pairing BepInEx\plugins\<GUID>.dll with BepInEx\config\<GUID>.cfg.
        public const string Guid = "com.community.hs.BgReconnect";
        public const string Name = "BG Reconnect";
        public const string Version = "1.0.0";
        public const string DownloadLink = "https://github.com/Nyverlight/Hearthstone_BG_reconnect_plugin";
        public const string Description = "Reconnect to a Battlegrounds match with a shortcut or a floating button";
    }

    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("Hearthstone.exe")]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;

        private ConfigEntry<KeyboardShortcut> hotkey;
        private ConfigEntry<bool> showButton;
        private ConfigEntry<float> buttonX;
        private ConfigEntry<float> buttonY;
        private ConfigEntry<float> cooldownSeconds;

        /// <summary>Set when a game API is missing (a Hearthstone patch renamed it): the mod then stays idle.</summary>
        private bool broken;
        private float lastReconnectAt = float.NegativeInfinity;

        // Cached once per frame by Update(), so OnGUI (called several times per frame) doesn't query the game repeatedly.
        private GameApi.Status status;

        private bool dragging;
        private Vector2 dragOffset;
        private GUIStyle buttonStyle;
        private int buttonStyleFontSize = -1;

        private void Awake()
        {
            Logger = base.Logger;

            // Metadata Firestone reads to show the mod in Settings > Mods.
            Config.Bind("General", "Name", PluginInfo.Name);
            Config.Bind("General", "Guid", PluginInfo.Guid);
            Config.Bind("General", "Version", PluginInfo.Version);
            Config.Bind("General", "DownloadLink", PluginInfo.DownloadLink);
            Config.Bind("General", "Description", PluginInfo.Description);

            hotkey = Config.Bind("Reconnect", "Hotkey",
                new KeyboardShortcut(KeyCode.W, KeyCode.LeftControl, KeyCode.LeftShift),
                "Shortcut that drops the Battlegrounds connection so Hearthstone reconnects.");
            cooldownSeconds = Config.Bind("Reconnect", "CooldownSeconds", 5f,
                "Minimum delay between two reconnects.");
            showButton = Config.Bind("Button", "ShowButton", true,
                "Show the floating Reconnect button during Battlegrounds matches.");
            buttonX = Config.Bind("Button", "ButtonX", 0.86f,
                "Horizontal position of the button, from 0 (left) to 1 (right). Right-drag the button to move it.");
            buttonY = Config.Bind("Button", "ButtonY", 0.02f,
                "Vertical position of the button, from 0 (top) to 1 (bottom).");

            Logger.LogInfo($"Plugin {PluginInfo.Guid} is loaded!");
        }

        private void Update()
        {
            if (broken)
            {
                return;
            }

            status = SafeGetStatus();
            if (hotkey.Value.IsDown())
            {
                TryReconnect("hotkey");
            }
        }

        private float CooldownRemaining =>
            Mathf.Max(0f, lastReconnectAt + cooldownSeconds.Value - Time.realtimeSinceStartup);

        private void TryReconnect(string source)
        {
            if (!status.InBattlegrounds)
            {
                Logger.LogDebug($"Reconnect ({source}) ignored: not in a Battlegrounds match");
                return;
            }
            if (status.Reconnecting || !status.Connected)
            {
                Logger.LogDebug($"Reconnect ({source}) ignored: not connected to the game server");
                return;
            }
            if (CooldownRemaining > 0f)
            {
                Logger.LogDebug($"Reconnect ({source}) ignored: cooldown");
                return;
            }

            try
            {
                GameApi.DropGameServerConnection();
                lastReconnectAt = Time.realtimeSinceStartup;
                Logger.LogInfo($"Reconnect triggered ({source})");
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private GameApi.Status SafeGetStatus()
        {
            try
            {
                return GameApi.GetStatus();
            }
            catch (Exception e)
            {
                Fail(e);
                return default;
            }
        }

        private void Fail(Exception e)
        {
            if (e is MissingMethodException || e is MissingFieldException || e is TypeLoadException)
            {
                broken = true;
                Logger.LogError($"Hearthstone's API changed, BG Reconnect is disabled until it is updated: {e.Message}");
            }
            else
            {
                Logger.LogError($"Unexpected error: {e}");
            }
        }

        private void OnGUI()
        {
            if (broken || !showButton.Value || !status.InBattlegrounds)
            {
                return;
            }

            float height = Mathf.Round(Screen.height * 0.04f);
            float width = Mathf.Round(height * 3.6f);
            var rect = new Rect(
                Mathf.Clamp(buttonX.Value, 0f, 1f) * (Screen.width - width),
                Mathf.Clamp(buttonY.Value, 0f, 1f) * (Screen.height - height),
                width,
                height);

            if (HandleDrag(ref rect, width, height))
            {
                return;
            }

            EnsureStyle(height);
            float cooldown = CooldownRemaining;
            string label;
            bool enabled;
            if (status.Reconnecting || !status.Connected)
            {
                label = "Reconnexion…";
                enabled = false;
            }
            else if (cooldown > 0f)
            {
                label = $"Reconnect ({Mathf.CeilToInt(cooldown)}s)";
                enabled = false;
            }
            else
            {
                label = "Reconnect";
                enabled = true;
            }

            bool previous = GUI.enabled;
            GUI.enabled = enabled;
            if (GUI.Button(rect, label, buttonStyle))
            {
                TryReconnect("button");
            }
            GUI.enabled = previous;
        }

        /// <summary>Right-drag moves the button; the position is saved on release. True while dragging.</summary>
        private bool HandleDrag(ref Rect rect, float width, float height)
        {
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 1 && rect.Contains(e.mousePosition):
                    dragging = true;
                    dragOffset = e.mousePosition - rect.position;
                    e.Use();
                    break;
                case EventType.MouseDrag when dragging:
                    float maxX = Mathf.Max(1f, Screen.width - width);
                    float maxY = Mathf.Max(1f, Screen.height - height);
                    Vector2 position = e.mousePosition - dragOffset;
                    buttonX.Value = Mathf.Clamp01(position.x / maxX);
                    buttonY.Value = Mathf.Clamp01(position.y / maxY);
                    e.Use();
                    break;
                case EventType.MouseUp when dragging && e.button == 1:
                    dragging = false;
                    Config.Save();
                    e.Use();
                    break;
            }

            if (dragging)
            {
                rect.position = new Vector2(buttonX.Value * (Screen.width - width), buttonY.Value * (Screen.height - height));
                GUI.Box(rect, "↔");
            }
            return dragging;
        }

        private void EnsureStyle(float height)
        {
            int fontSize = Mathf.Max(10, Mathf.RoundToInt(height * 0.42f));
            if (buttonStyle != null && buttonStyleFontSize == fontSize)
            {
                return;
            }
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
            };
            buttonStyleFontSize = fontSize;
        }
    }

    /// <summary>
    /// Every call into Hearthstone's code lives here, in non-inlined methods, so that a member removed by a game patch
    /// throws a catchable MissingMethodException at the call site instead of breaking the plugin's own methods.
    /// </summary>
    internal static class GameApi
    {
        public struct Status
        {
            public bool InBattlegrounds;
            public bool Connected;
            public bool Reconnecting;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Status GetStatus()
        {
            var status = new Status();
            GameMgr gameMgr = GameMgr.Get();
            GameState gameState = GameState.Get();
            if (gameMgr == null || gameState == null)
            {
                return status;
            }

            status.InBattlegrounds =
                gameMgr.IsBattlegrounds() && !gameMgr.IsSpectator() && !gameState.IsGameOverNowOrPending();
            if (!status.InBattlegrounds)
            {
                return status;
            }

            Network network = Network.Get();
            ReconnectMgr reconnectMgr = ReconnectMgr.Get();
            status.Connected = network != null && network.IsConnectedToGameServer();
            status.Reconnecting = reconnectMgr != null && reconnectMgr.IsReconnectingToGame();
            return status;
        }

        /// <summary>
        /// Closes the game-server socket without flagging the disconnect as requested, so Hearthstone treats it as a
        /// network drop and its ReconnectMgr rejoins the same match.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void DropGameServerConnection()
        {
            Network.Get().SimulateUncleanDisconnectFromGameServer();
        }
    }
}
