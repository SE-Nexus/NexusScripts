using NexusAPI.Utilities;
using NGPlugin.BoundarySystem;
using NGPlugin.Scripts;
using NGPlugin.BoundarySystem.Sectors;
using NGPlugin.Config;
using NGPlugin.Messages;
using NGPlugin.Utilities;
using NLog;
using Sandbox;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Planet;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;

namespace NGPlugin.Scripts.ExampleScripts
{
    public class GridSpawnOnPlanet2 : ScriptAPI
    {

        bool SmallGridsOnly = false;
        bool LargeGridsOnly = false;

        public override bool InboundGate(MyCubeGrid BiggestGrid, List<MyCubeGrid> AllGrids, GateAPI Gate, List<long> allPlayers, ref Vector3D gridSpawnPos, ref byte targetServer)
        {
            /*  Inbound gate filter. This is how you can restrict travel into a gate. (Return true for allowing transfer, false for blocking transfer).
             *  
             *      Method paramaters:
             *          BiggestGrid: The biggest grid in the transfer. Usually the main grid.
             *          AllGrids: All grids in the transfer. This is including connecitons/subgrids.
             *          Gate: The gate object that is being used/travelled into. (Spawn positions etc)
             *          AllPlayers: List of playerids sitting in the seats of all grids
             *          GridSpawnPos: The position the grid will spawn at. You can modify this to change the spawn position.
             *          TargetServer: The server the grid will be transferred to. You can modify this to change the target server overriding its configured value.
             * 
             * 
             * 
             */



            if (SmallGridsOnly && BiggestGrid.GridSizeEnum == MyCubeSize.Large)
            {
                return false;
            }

            if (LargeGridsOnly && BiggestGrid.GridSizeEnum == MyCubeSize.Small)
            {
                return false;
            }



            return true;
        }

        public override void OutboundGate(IEnumerable<MyCubeGrid> grids, GateAPI fromGate)
        {

            /*
             *  Spawned Grid Actions. After the grid has been spawned, you can perform actions on the grid.
             * 
             * 
             */

        }
    }
}