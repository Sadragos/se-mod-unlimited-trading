using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Timers;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.ModAPI;
using UnlimitedTrading;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using Sandbox.Game.World;

//using Sandbox.ModAPI.Ingame;

namespace UnlimitedTrading
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation)]
    public class Core : MySessionComponentBase
    {
        // Declarations
        private static readonly string version = "v1.0";

        private static bool _initialized;

        public const ushort CLIENT_ID = 1670;
        public const ushort SERVER_ID = 1671;

        private const int timeout = 60 * 60 * 2;
        private static int interval = 0;
        private IEnumerator<bool> UpdateRun;


        // Initializers
        private void Initialize()
        {
            // Chat Line Event
            AddMessageHandler();


            Logging.Instance.WriteLine(string.Format("Script Initialized: {0}", version));

        }

        // CLIENT hat eine Chatnachricht eingegeben. Prüfe, ob es ein Befehl ist
        public void HandleMessageEntered(string messageText, ref bool sendToOthers)
        {
            byte[] data = null;
            Logging.Instance.WriteLine("HandleMessageEntered " + messageText);

            if (messageText.StartsWith("/trade"))
            {
                data = Utilities.MessageToBytes(new MessageData()
                {
                    SteamId = MyAPIGateway.Session.Player.SteamUserId,
                    Message = messageText.Replace("/trade", "").Trim()
                });
                sendToOthers = false;
            }

            if (data != null)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    MyAPIGateway.Multiplayer.SendMessageToServer(SERVER_ID, data);
                });
            }

        }

        // CLIENT hat Daten vom Server Erhalten. Entweder Chatnachricht oder Dialog
        public void HandleServerData(byte[] data)
        {
            Logging.Instance.WriteLine(string.Format("Received Server Data: {0} bytes", data.Length));
            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Session.LocalHumanPlayer == null)
                return;

            MessageData item = Utilities.BytesToMessage(data);

            if (item == null)
                return;

            if (item.Type.Equals("chat"))
            {
                MyAPIGateway.Utilities.ShowMessage(item.Sender, item.Message);
            }
            else if (item.Type.Equals("dialog"))
            {
                Dialog(item.Message, item.DialogTitle);
            }
        }

        // SERVER hat Daten vom Spieler erhalten - Vermutlich ein Befehl
        public void HandlePlayerData(byte[] data)
        {
            Logging.Instance.WriteLine(string.Format("Received Player Data: {0} bytes", data.Length));
            MessageData request = Utilities.BytesToMessage(data);
            if (request == null)
                return;
            if (request.Message.Equals("refresh"))
            {
                Logging.Instance.WriteLine("Refresh Command");
                IMyPlayer player = Utilities.GetPlayer(request.SteamId);
                if (player == null || player.PromoteLevel.CompareTo(MyPromoteLevel.Admin) < 0)
                    return;
                StartUpdate();
                Utilities.ShowChatMessage("Started Store Refresh", player.IdentityId);
            }
        }

        // Zeigt dem Spieler eine Dialog an
        public static void Dialog(string message, string title = null)
        {
            if (title == null) title = "Trading";
            MyAPIGateway.Utilities.ShowMissionScreen(title, "", "", message.Replace("|", "\n\r"));
        }

        public void AddMessageHandler()
        {
            //register all our events and stuff
            MyAPIGateway.Utilities.MessageEntered += HandleMessageEntered;
            MyAPIGateway.Multiplayer.RegisterMessageHandler(CLIENT_ID, HandleServerData);
            MyAPIGateway.Multiplayer.RegisterMessageHandler(SERVER_ID, HandlePlayerData);
        }

        public void RemoveMessageHandler()
        {
            //unregister them when the game is closed
            MyAPIGateway.Utilities.MessageEntered -= HandleMessageEntered;
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(CLIENT_ID, HandleServerData);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(SERVER_ID, HandlePlayerData);
        }

        public void StartUpdate()
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            UpdateRun = Updater();
        }

        private IEnumerator<bool> Updater()
        {
            Logging.Instance.WriteLine("Starting Refresh Cycle");

            HashSet<VRage.ModAPI.IMyEntity> Grids = new HashSet<VRage.ModAPI.IMyEntity>();
            List<MyStoreQueryItem> stock = new List<MyStoreQueryItem>();
            HashSet<Sandbox.ModAPI.IMyStoreBlock> stores = new HashSet<Sandbox.ModAPI.IMyStoreBlock>();
            HashSet<Sandbox.ModAPI.IMyTextPanel> panels = new HashSet<Sandbox.ModAPI.IMyTextPanel>();
            List<VRage.Game.ModAPI.IMySlimBlock> slimBlocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
            Dictionary<string, List<Line>> StoreDisplays = new Dictionary<string, List<Line>>();
            yield return true;


            MyAPIGateway.Entities.GetEntities(Grids, entity => entity is VRage.Game.ModAPI.IMyCubeGrid);
            yield return true;

            foreach (VRage.Game.ModAPI.IMyCubeGrid grid in Grids)
            {
                slimBlocks.Clear();
                grid.GetBlocks(slimBlocks, b => b.FatBlock is Sandbox.ModAPI.IMyTerminalBlock);
                yield return true;
                foreach (VRage.Game.ModAPI.IMySlimBlock slim in slimBlocks)
                {
                    Sandbox.ModAPI.IMyTerminalBlock terminal = slim.FatBlock as Sandbox.ModAPI.IMyTerminalBlock;
                    if (terminal is Sandbox.ModAPI.IMyStoreBlock && GetStorePass(terminal) != null)
                    {
                        stores.Add(terminal as Sandbox.ModAPI.IMyStoreBlock);
                    }
                    else if (terminal is Sandbox.ModAPI.IMyTextPanel && GetStorePass(terminal) != null)
                    {
                        panels.Add(terminal as Sandbox.ModAPI.IMyTextPanel);
                    }
                }
                yield return true;
            }


            foreach (Sandbox.ModAPI.IMyStoreBlock store in stores)
            {
                string stockOverride = GetStoreStock(store);
                string gpsname = GetStoreGPS(store);
                List<Line> lines = new List<Line>();

                lines.Add(new Line(string.Format("# {0,8}", DateTime.Now.ToString("HH:mm")), store.CustomName));

                for (int i = 0; i < 5; i++)
                {
                    stock.Clear();
                    store.GetPlayerStoreItems(stock);
                    stock.RemoveAll(s => s.Amount == 0 || s.PricePerUnit == 0);
                    if (stock.Count > 0) break;
                    else yield return true;
                }

                if (stock.Count > 0)
                {
                    lines.Add(new Line(string.Format("~\u252c{0}\u252c{1}", new string('\u2500', 8), new string('\u2500', 7))));
                    lines.Add(new Line(string.Format("# \u2502 {0,6} \u2502 {1,6}", "Stock", "Price"), "Product"));
                    lines.Add(new Line(string.Format("~\u253c{0}\u253c{1}", new string('\u2500', 8), new string('\u2500', 7))));
                    stock.Sort((a, b) => ItemName(a).CompareTo(ItemName(b)));
                    foreach (MyStoreQueryItem item in stock)
                    {
                        lines.Add(new Line(string.Format("# \u2502 {0,6} \u2502 {1,6}", stockOverride == null ? Utilities.FormatCredits(item.Amount) : stockOverride, Utilities.FormatCredits(item.PricePerUnit)), ItemName(item)));
                    }
                    lines.Add(new Line(string.Format("~\u2534{0}\u2534{1}", new string('\u2500', 8), new string('\u2500', 7))));
                }
                else
                {
                    lines.Add(new Line("~"));
                    lines.Add(new Line("Currently no Stock")); ;
                    lines.Add(new Line("~"));

                }
                if (gpsname != null)
                {
                    Vector3D pos = store.GetPosition();
                    lines.Add(new Line(string.Format("GPS:{0}:{1:0}:{2:0}:{3:0}:", gpsname, pos.X, pos.Y, pos.Z)));
                }
                if(StoreDisplays.ContainsKey(GetStorePass(store)))
                {
                    StoreDisplays.Remove(GetStorePass(store));
                }
                StoreDisplays.Add(GetStorePass(store), lines);
                Logging.Instance.WriteLine("Updated Stock of " + store.CubeGrid.CustomName + " - > " + store.CustomName);
                yield return true;
            }


            yield return true;

            foreach (Sandbox.ModAPI.IMyTextPanel panel in panels)
            {
                string id = GetStorePass(panel as Sandbox.ModAPI.IMyTerminalBlock);
                if (id != null && StoreDisplays.ContainsKey(id))
                {
                    float fontSize = panel.MeasureStringInPixels(new StringBuilder("-"), "Monospace", panel.FontSize).X;
                    Logging.Instance.WriteLine("Font Size " + fontSize);
                    Logging.Instance.WriteLine("SurfaceSize " + panel.SurfaceSize.X);
                    Logging.Instance.WriteLine("TextPadding " + panel.TextPadding);
                    int width = (int)Math.Floor((panel.SurfaceSize.X * (1-panel.TextPadding/50f)) / fontSize);
                    Logging.Instance.WriteLine("width " + width);
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    panel.Font = "Monospace";
                    StringBuilder sb = new StringBuilder();
                    foreach (Line line in StoreDisplays[id])
                    {
                        sb.Append(line.Print(width));
                        sb.Append('\n');
                    }
                    panel.WriteText(sb.ToString());
                    Logging.Instance.WriteLine("Updated Display of " + panel.CubeGrid.CustomName + " - > " + panel.CustomName);
                    yield return true;
                }
            }

            Logging.Instance.WriteLine("Updated " + stores.Count + " Stores and " + panels.Count + " Panels");
        }

        private static string LimitLength(string input, int length)
        {
            return input != null && input.Length > length ? input.Substring(0, length) : input;
        }

        private string ItemName(MyStoreQueryItem item, int length = 18)
        {
            string result = item.ItemId.ToString();
            result = result.Replace("MyObjectBuilder_Component/", "");
            result = result.Replace("MyObjectBuilder_AmmoMagazine/", "");
            result = result.Replace("MyObjectBuilder_PhysicalGunObject/", "");
            result = result.Replace("MyObjectBuilder_PhysicalObject/", "");
            result = result.Replace("MyObjectBuilder_Datapad/", "");
            result = result.Replace("MyObjectBuilder_ConsumableItem/", "");
            result = result.Replace("MyObjectBuilder_OxygenContainerObject/", "");
            result = result.Replace("MyObjectBuilder_GasContainerObject/", "");
            result = result.Replace("MyObjectBuilder_Ore", "Ore");
            result = result.Replace("MyObjectBuilder_Ingot", "Ingot");
            result = LimitLength(result, length);
            return result;
        }

        public string GetStorePass(Sandbox.ModAPI.Ingame.IMyTerminalBlock store)
        {
            return GetStoreWord(store, "UpdateName: ");
        }

        public string GetStoreStock(Sandbox.ModAPI.Ingame.IMyTerminalBlock store)
        {
            return GetStoreWord(store, "Stock: ");
        }

        public string GetStoreGPS(Sandbox.ModAPI.Ingame.IMyTerminalBlock store)
        {
            return GetStoreWord(store, "GPS: ");
        }

        public string GetStoreWord(Sandbox.ModAPI.Ingame.IMyTerminalBlock store, string word)
        {
            if (store.CustomData == null || !store.CustomData.Contains(word)) return null;
            string[] lines = store.CustomData.Split('\n');
            foreach (string l in lines)
            {
                if (l.StartsWith(word))
                {
                    return l.Replace(word, "");
                }
            }
            return null;
        }

        // Overrides
        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (!MyAPIGateway.Multiplayer.IsServer)
                    return;

                if (MyAPIGateway.Session == null)
                    return;

                // Run the init
                if (!_initialized)
                {
                    _initialized = true;
                    Initialize();
                }
                else if (UpdateRun != null && interval % 5 == 0 && !UpdateRun.MoveNext())
                {
                    UpdateRun = null;
                }
                else if (interval++ >= timeout)
                {
                    interval = 0;
                    StartUpdate();
                }

            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("UpdateBeforeSimulation(): {0}", ex));
            }
        }

        public class Line
        {
            string LongText;
            string FormatString;

            public Line(string formatString, string longText = null)
            {
                LongText = longText;
                FormatString = formatString;
            }

            public string Print(int panelwidth)
            {
                int remaining = Math.Max(1, panelwidth - FormatString.Length - 1);
                if (FormatString.Contains("#"))
                {
                    return string.Format(FormatString.Replace("#", "{0,-" + remaining + "}"), LimitLength(LongText, remaining));
                }
                else if (FormatString.Contains("~"))
                {
                    return FormatString.Replace("~", new string('\u2500', remaining));
                }
                return FormatString;
            }
        }


        public override void UpdateAfterSimulation()
        {
        }

        protected override void UnloadData()
        {
            try
            {
                RemoveMessageHandler();

                if (Logging.Instance != null)
                    Logging.Instance.Close();
            }
            catch
            {
            }

            base.UnloadData();
        }
    }
}