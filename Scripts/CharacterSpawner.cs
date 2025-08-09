using NexusAPI.Messages;
using NGPlugin.BoundarySystem;
using NGPlugin.Scripts;
using NGPlugin.Config;
using NGPlugin.Messages;
using NLog;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;
using VRage.Game;
using VRageMath;

namespace NGPlugin.Scripts.ExampleScripts
{
    /// <summary>
    /// Spawns a character at a specific point
    /// </summary>
    public class CharacterSpawner : ScriptAPI
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();


        /* Units are in meters */
        private double X_Component = 250000;
        private double Y_Component = 250000;
        private double Z_Component = 250000;



        public CharacterSpawner() { }

        public override Task InboundSpawnPad_Pre(ScriptSpawnMessage spawnReqMsg)
        {

            //If we clear the grids, then this will only spawn the characters at a specified position
            spawnReqMsg.SpawningGrids.Clear();

            Vector3D targetPosition = new Vector3D(X_Component, Y_Component, Z_Component);

            foreach(PlayerItem spawningPlayer in spawnReqMsg.playerOBData)
            {
                spawningPlayer.SetPlayerPosition(targetPosition);
                Log.Info($"Spawning character {spawningPlayer.PlayerName} AT {spawningPlayer.Position}");
            }

            return Task.CompletedTask;
        }

    }
}
