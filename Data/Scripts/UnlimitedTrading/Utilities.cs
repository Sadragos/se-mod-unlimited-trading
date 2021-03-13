using System;
using System.IO;
using System.Text;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using Sandbox.Game.Weapons;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using IMySlimBlock = VRage.Game.ModAPI.IMySlimBlock;
using VRage.Game.ModAPI.Interfaces;
using Sandbox.Common.ObjectBuilders;
using UnlimitedTrading;

namespace UnlimitedTrading
{
    internal class Utilities
    {
        public static IMyIdentity EntityToIdentity(IMyEntity entity)
        {
            if (entity == null) { return null; }

            if (entity is IMyCharacter)
            {
                return CharacterToIdentity((IMyCharacter)entity);
            }
            else if (entity is IMyEngineerToolBase)
            {
                var tool = (IMyEngineerToolBase)entity;
                if (tool == null) { return null; }

                var toolOwner = MyAPIGateway.Entities.GetEntityById(tool.OwnerId);
                if (toolOwner == null) { return null; }

                var character = (IMyCharacter)toolOwner;
                if (character == null) { return null; }

                return CharacterToIdentity(character);
            }
            else if (entity is MyCubeBlock)
            {
                var block = (MyCubeBlock)entity;
                if (block == null) { return null; }
                return CubeBlockOwnerToIdentity(block);
            }
            else if (entity is IMyGunBaseUser)
            {
                var weapon = (IMyGunBaseUser)entity;
                if (weapon == null) { return null; }

                var weaponOwner = weapon.Owner;
                if (weaponOwner == null) { return null; }

                var character = (IMyCharacter)weaponOwner;
                if (character == null) { return null; }

                return CharacterToIdentity(character);
            }
            else if (entity is IMyCubeGrid)
            {
                return GridToIdentity((IMyCubeGrid)entity);
            } 
            else if(entity.GetType().Name == "MyMissile")
            {
                try
                {
                    MyObjectBuilder_Missile builder = (MyObjectBuilder_Missile)entity.GetObjectBuilder();
                    var identities = new List<IMyIdentity>();
                    MyAPIGateway.Players.GetAllIdentites(identities, i => i.IdentityId == builder.Owner);
                    return identities.FirstOrDefault();
                } catch (Exception e)
                {
                    Logging.Instance.WriteLine("Could not parse missileentity");
                }
            }

            return null;
        }

        public static string FormatCredits(long credits)
        {
            if (credits < 10000) return credits.ToString();
            if (credits < 100000) return (credits / 1000f).ToString("0.0") + "k";
            if (credits < 1000000) return (credits / 1000f).ToString("0") + "k";
            if (credits < 10000000) return (credits / 1000000f).ToString("0.0") + "m";
            return (credits / 1000000f).ToString("0") + "m";
        }

        public static IMyIdentity CharacterToIdentity(IMyCharacter character)
        {
            if (character == null) { return null; }
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, p => p.Character == character);
            var player = players.FirstOrDefault();
            if (player == null) { return null; }
            return player.Identity;
        }

        public static IMyIdentity CubeBlockOwnerToIdentity(IMyCubeBlock block)
        {
            if (block == null) { return null; }
            var identities = new List<IMyIdentity>();
            MyAPIGateway.Players.GetAllIdentites(identities, i => i.IdentityId == block.OwnerId);
            return identities.FirstOrDefault();
        }

        public static void ShowChatMessage(string message, long playerid = 0)
        {
            //this takes the death message and sends it out to all players
            Logging.Instance.WriteLine("ShowMessage " + message);

            //only the server should do this
            if (!MyAPIGateway.Multiplayer.IsServer)
            {
                return;
            }
            MyVisualScriptLogicProvider.SendChatMessageColored(message, VRageMath.Color.OrangeRed, (playerid != 0 ? "~" : "")+"Trading", playerid);
        }

        public static IMyIdentity GridToIdentity(IMyCubeGrid grid)
        {
            if (grid == null) { return null; }

            var gridOwnerId = grid.BigOwners.FirstOrDefault();
            var identities = new List<IMyIdentity>();
            MyAPIGateway.Players.GetAllIdentites(identities, i => i.IdentityId == gridOwnerId);
            var ownerIdentity = identities.FirstOrDefault();
            if (ownerIdentity != null) { return ownerIdentity; }

            // can't find owner, go by the first built by
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);
            var block = blocks.FirstOrDefault();
            if (block == null || !(block is MyCubeBlock)) { return null; }
            return CubeBlockBuiltByToIdentity(((MyCubeBlock)block).BuiltBy);
        }

        public static IMyIdentity CubeBlockBuiltByToIdentity(long builtBy)
        {
            var identities = new List<IMyIdentity>();
            MyAPIGateway.Players.GetAllIdentites(identities, i => i.IdentityId == builtBy);
            return identities.FirstOrDefault();
        }

        public static string TagPlayerName(IMyIdentity identity)
        {
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(identity.IdentityId);
            return (faction != null) ? (faction.Tag + "." + identity.DisplayName) : identity.DisplayName;
        }

        public static IMyPlayer IdentityToPlayer(IMyIdentity identity)
        {
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, i => i.IdentityId == identity.IdentityId);
            return players.FirstOrDefault();
        }

        public static byte[] MessageToBytes(MessageData data)
        {
            try
            {
                string itemMessage = MyAPIGateway.Utilities.SerializeToXML(data);
                byte[] itemData = Encoding.UTF8.GetBytes(itemMessage);
                return itemData;
            } catch (Exception e)
            {
                Logging.Instance.WriteLine(e.ToString());
                return null;
            }
        }

        public static long PayFaction(IMyFaction faction, long amount)
        {
            if (amount < 0)
            {
                long balance = 0;
                if (faction.TryGetBalanceInfo(out balance))
                {
                    balance = Math.Max(-balance, amount);
                    faction.RequestChangeBalance(balance);
                    return balance;
                }
                return 0;
            }
            else
            {
                faction.RequestChangeBalance(amount);
                return amount;
            }
        }

        public static long PayPlayer(IMyPlayer player, long amount)
        {
            if(amount < 0)
            {
                long balance = 0;
                if(player.TryGetBalanceInfo(out balance))
                {
                    balance = Math.Max(-balance, amount);
                    player.RequestChangeBalance(balance);
                    return balance;
                }
                return 0;
            } else
            {
                player.RequestChangeBalance(amount);
                return amount;
            }
        }

        public static void SendMessageToClient(MessageData data, ulong steamid)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                MyAPIGateway.Multiplayer.SendMessageTo(Core.CLIENT_ID, MessageToBytes(data), steamid);
            });
        }

        public static MessageData BytesToMessage(byte[] bytes)
        {
            try
            {
                string itemMessage = Encoding.UTF8.GetString(bytes);
                MessageData itemData = MyAPIGateway.Utilities.SerializeFromXML<MessageData>(itemMessage);
                return itemData;
            }
            catch (Exception e)
            {
                Logging.Instance.WriteLine(e.ToString());
                return null;
            }
        }

        public static string CurrentTimestamp()
        {
            return DateTime.Now.ToString("dd.MM.yy HH:mm:ss");
        }

        public static IMyPlayer GetPlayer(ulong steamid)
        {
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, i => i.SteamUserId == steamid);
            return players.FirstOrDefault();
        }

        public static IMyPlayer GetPlayer(string name)
        {
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, i => i.DisplayName.ToLower().Equals(name.ToLower()));
            return players.FirstOrDefault();
        }
    }
}