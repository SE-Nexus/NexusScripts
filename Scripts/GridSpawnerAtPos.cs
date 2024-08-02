using NGPlugin.Messages;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace NGPlugin.Scripts.ExampleScripts
{
    public class GridSpawnerAtPos : ScriptAPI
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /* Units are in meters */
        private double X_Component = 250000;
        private double Y_Component = 250000;
        private double Z_Component = 250000;

        /* Spawn Checks on Grids are done automatically to detect any collisions. */


        public override Task InboundSpawnPad_Pre(ScriptSpawnMessage spawnReqMsg)
        {
            spawnReqMsg.GridSpawnPosition = new Vector3D(X_Component, Y_Component, Z_Component);
            return Task.CompletedTask;
        }

    }
}
