using LitJson;
using NGPlugin.Config;
using NGPlugin.Scripts;
using NLog;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Net.Http;
using VRageMath;

namespace NexusSpawnScripts.Scripts
{
    // This script is a sample of how to use the AuthenticationGateFilter to prevent users from transferring to an instance through a portal.
    // It also has a sample of slight location randomization so grids don not spawn right on top of each other on the portal entry and exit.
    public class AuthenticationGateFilter : ScriptAPI
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static Random rnd = new Random();

        public override bool InboundGate(MyCubeGrid BiggestGrid, List<MyCubeGrid> AllGrids, GateAPI Gate, ref Vector3D gridSpawnPos, ref byte targetServer)
        {
            foreach (var grid in AllGrids)
            {
                foreach (var bigOwner in grid.BigOwners)
                {
                    var steamId = MySession.Static.Players.TryGetSteamId(bigOwner);
                    var identity = MySession.Static.Players.TryGetIdentity(bigOwner);
                    if (CheckIfSupporter(steamId.ToString())) continue;
                    MyVisualScriptLogicProvider.SendChatMessage($"Grid owner {identity.DisplayName} is not a supporter. Can not transfer grid.", "", identity.IdentityId);
                    Log.Warn($"Grid owner {identity.DisplayName} is not a supporter. Can not transfer grid.");
                    return false;
                }

                foreach (var cockpit in grid.OccupiedBlocks.ToArray())
                {
                    cockpit.Pilot.GetPlayerId(out var playerId);
                    var identity = cockpit.Pilot.GetIdentity();
                    var steamId = playerId.SteamId;
                    if (CheckIfSupporter(steamId.ToString())) continue;
                    MyVisualScriptLogicProvider.SendChatMessage($"Mounted player {identity.DisplayName} is not a supporter. Can not transfer grid.", "", identity.IdentityId);
                    Log.Warn($"Mounted player {identity.DisplayName} is not a supporter. Can not transfer grid.");
                    return false;
                }
            }

            try
            {
                // generate random direction
                var randomDir = RandomDirection();

                // generate random distance in meters
                var randomDist = RandomNumberBetween(1500, 6000);

                // generate new position*
                var generatedPosition = randomDir * randomDist;

                // add generated position to Target
                Log.Warn($"Calculated Transform: {generatedPosition}");
                Transform(ref gridSpawnPos, generatedPosition);
                Log.Warn($"New Calculated Pos: {gridSpawnPos}");
                //Target = Target + generatedPosition;

                foreach (var gridInGroup in AllGrids)
                {
                    if (gridInGroup == null || gridInGroup.Physics == null)
                        continue;

                    gridInGroup.Physics.LinearVelocity = new Vector3(0, 0, 0);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            //Allow it to pass
            Log.Warn($"Successfully transferred {BiggestGrid.Name}, ");
            return true;
        }


        public bool CheckIfSupporter(string uSteamid)
        {
            HttpResponseMessage response;
            using (var clients = new HttpClient())
            {
                var pairs = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("STEAMID", uSteamid)
                };
                var content = new FormUrlEncodedContent(pairs);
                var task = clients.PostAsync("[URL HERE]", content);

                response = task.GetAwaiter().GetResult();
                var result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var json = JsonMapper.ToObject(result);
                if (!json.ContainsKey("supporterLevel")) return false;
                var level = (int)json["supporterLevel"];
                var isSupporter = level > 0;
                return isSupporter;
            }
        }

        public void Transform(ref Vector3D Original, Vector3D Transform)
        {
            Original.X += Transform.X;
            Original.Y += Transform.Y;
            Original.Z += Transform.Z;
        }

        private static Vector3D RandomDirection()
        {
            var randomDir = new Vector3D(rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5);
            randomDir = Vector3D.Normalize(randomDir);
            return randomDir;
        }

        private static double RandomNumberBetween(double minValue, double maxValue)
        {
            return minValue + (rnd.NextDouble() * (maxValue - minValue));
        }
    }
}
