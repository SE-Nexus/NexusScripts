using NexusAPI.Utilities;
using NGPlugin.Scripts;
using NGPlugin.BoundarySystem;
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
    public class GridSpawnOnPlanet : ScriptAPI
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();


        /*  Following bit will prioritize spawning near faction mates over the selected enum.
           *      If no one is online, it will continue with the SelectedOption
           *  
           */

        private bool PrioritizeFactionMembers = false; //Will attempt to spawn near online faction members on planet (WIP)
        private bool PrioritizeAtmosphere = true; //Will attempt to find a planet with atmo first
        private string TargetPlanet = ""; //Specify a target planet. Leave blank for random on server
        private float SpawnHeight = 50; //Leave 0 to spawn as close to the surface as possible
        private float MinAirDensity = 0f;
        private int MaxIterations = 20;


        /* DO NOT MODIFY BELOW */
        private Random rand = new Random(new Guid().GetHashCode());
        private ScriptSpawnMessage spawnReqMsg;
        private long IdentityID;
        private ServerAPI TargetServer;
        private SectorAPI TargetSector;


        public override async Task InboundSpawnPad_Pre(ScriptSpawnMessage spawnReqMsg)
        {

            /* Get Target Server from Region Handler */
            this.spawnReqMsg = spawnReqMsg;
            IdentityID = spawnReqMsg.playerOBData.First().IdentityID;

            if (!RegionHandler.TryGetServer(RegionHandler.ThisServer.ServerID, out TargetServer))
                return;

            List<MyPlanet> allOptions = GetTargetPlanets(TargetServer);
            if (allOptions.Count <= 0)
                return;


            BoundingBox ShipBox = GetSpawnShipBox(spawnReqMsg.SpawningGrids);
            for (int i = 0; i < MaxIterations; i++)
            {
                //Get first index after shuffle
                MyPlanet pTarget = allOptions.GetRandomItemFromList();

                //Prioritize Atmo
                if (!pTarget.HasAtmosphere && PrioritizeAtmosphere && MaxIterations > MaxIterations / 2)
                    continue;



                Log.Info($"Attempting to spawn grids @ {pTarget.StorageName}. Iteration: {i}");
                Vector3D position = await AsyncInvoke.InvokeAsync(() => TryGetPlanetSpawn(pTarget, TargetServer, ShipBox.Extents.Length()));


                //Only run this check if the server is sectored
                if (RegionHandler.ThisServer.SectorType == NexusAPI.ConfigAPI.ServerType.SyncedSectored)
                {
                    if (!RegionHandler.TryGetSmallestSector(position, out var sector))
                        continue;


                    if (sector.Data.OnServerID != TargetServer.ServerID)
                    {
                        Log.Info($"No valid spawn position for {pTarget.StorageName} after {MaxIterations} attempts");
                        continue;
                    }
                }

                

                //Found Valid position
                if (position != null && position != Vector3D.Zero)
                {
                    spawnReqMsg.GridSpawnPosition = position;
                    return;
                }
            }


            /* Separate logic here to check if its null or no options found */
            Log.Info("No Valid Spawn Position found. Try adjusting limits or playing with sector borders.");
            return;
        }

        public List<MyPlanet> GetTargetPlanets(ServerAPI TargetServer)
        {
            // Attempt to get target planet from name
            List<MyPlanet> myPlanets = new List<MyPlanet>();

            if (!string.IsNullOrEmpty(TargetPlanet))
            {
                myPlanets = MyPlanets.GetPlanets().Where(x => x.StorageName != null && x.StorageName.Contains(TargetPlanet, StringComparison.OrdinalIgnoreCase)).ToList();

                if (myPlanets == null || myPlanets.Count == 0)
                {
                    Log.Fatal($"Failed to find a planet with the name of: \"{TargetPlanet}\". One will be provided automatically... Did you mean: \n");
                    foreach (var planet in MyPlanets.GetPlanets())
                    {
                        Log.Fatal($"{planet.StorageName}?");
                    }

                }
                else
                    return myPlanets;


                myPlanets.Clear();
            }


            // Check to see if the target server even has sectors
            if (TargetServer.SectorType == NexusAPI.ConfigAPI.ServerType.SyncedSectored)
            {
                Log.Info($"Total Planets: {MyPlanets.GetPlanets().Count}");
                //Search all planets and see if any are inside of our servers sectors
                foreach (MyPlanet ent in MyPlanets.GetPlanets())
                {

                    SphereSector PlanetVolume = new SphereSector(ent.PositionComp.WorldAABB.Center, ent.MaximumRadius);
                    if (RegionHandler.TryGetSmallestSector(PlanetVolume, out TreeNode<SectorAPI> smallestSector) && smallestSector.Data.OnServerID == TargetServer.ServerID)
                        myPlanets.Add(ent);

                }

                //foreach (var planet in myPlanets)
                //    Log.Info($"{planet.Name} is valid spawn location!");

                if (myPlanets == null || myPlanets.Count == 0)
                {
                    Log.Fatal($"Failed to find a planet that is on the target server.");
                    return myPlanets;
                }


                /* Now that we have a list of possible planets to spawn, we need to generate possible spawn positions and then
                 * check to see if that actual position is on our server. (Sectors and overlap/intersect planets not just be contained completely)
                 * 
                 * 
                 */



            }
            else
            {

                // Non-sectored
                myPlanets = MyPlanets.GetPlanets();
                if (myPlanets.Count == 0)
                    throw new Exception("Server has no planets!");
            }


            //Null check?


            return myPlanets;
        }


        public Vector3D TryGetPlanetSpawn(MyPlanet planet, ServerAPI targetServer, float collisionRadius)
        {

            /* First, find a planet in the target server regions */
            Vector3D center = planet.PositionComp.WorldAABB.Center;
            int distanceIteration = 0;


            Vector3D globalPos = Vector3D.Zero;

            for (int i = 0; i < MaxIterations; i++)
            {
                MyFaction faction = MySession.Static.Factions.GetPlayerFaction(IdentityID);

                //Check if there are any active faction members online and near the planet
                if (PrioritizeFactionMembers && faction != null)
                {
                    foreach (var member in MySession.Static.Players.GetOnlinePlayers())
                    {
                        Vector3D Position = member.GetPosition();

                        //Note, this just gets the CLOSEST planet. Not actually if the player is on the planet
                        if (faction.Members.ContainsKey(member.Identity.IdentityId) && MyPlanets.Static.GetClosestPlanet(Position) == planet)
                        {
                            globalPos = member.GetPosition();
                        }
                    }
                }
                else
                {
                    Vector3D randomPerpendicularVector = MyUtils.GetRandomVector3D().Normalized();
                    globalPos = center + (randomPerpendicularVector * planet.MaximumRadius);
                }


                globalPos = planet.GetClosestSurfacePointGlobal(ref globalPos);

                //Log.Info($"Target Server: {targetServer.ServerID}. Sectors {targetServer.HasSectors}");
                /* Check to see if position is actually in our server */
                if (targetServer.HasSectors)
                {
                    RegionHandler.TryGetSmallestSector(globalPos, out var sector);
                    //Log.Info($"Position: {globalPos.ToString("0.00")}, OnServer: {sector.Data.OnServerID}");
                    if (sector.Data.OnServerID != targetServer.ServerID)
                        continue;
                }


                Vector3 vector = MyGravityProviderSystem.CalculateNaturalGravityInPoint(globalPos);
                if (Vector3.IsZero(vector))
                {
                    vector = Vector3.Up;
                }
                Vector3D vector3D = Vector3D.Normalize(vector);
                Vector3D vector3D2 = -vector3D;

                //Set Spawn Height
                globalPos = globalPos + vector3D2 * SpawnHeight;

                if (!TestPlanetLandingPosition(planet, globalPos, collisionRadius, true, ref distanceIteration))
                {
                    if (distanceIteration > 20)
                    {
                        break;
                    }
                    continue;
                }


            }

            return globalPos;
        }





        public static bool IsTerrainEven(MyPlanet planet, Vector3D targetPos, Vector3D gravityVector, float collisionRadius)
        {

            Vector3D deviationNormal = MyUtils.GetRandomPerpendicularVector(ref gravityVector);
            Vector3 vector = (Vector3)deviationNormal * collisionRadius;
            Vector3 vector2 = Vector3.Cross(vector, gravityVector);
            MyOrientedBoundingBoxD myOrientedBoundingBoxD = new MyOrientedBoundingBoxD(targetPos, new Vector3D(collisionRadius * 2f, Math.Min(10f, collisionRadius * 0.5f), collisionRadius * 2f), Quaternion.CreateFromForwardUp(deviationNormal, gravityVector));
            int num = -1;
            for (int i = 0; i < 4; i++)
            {
                num = -num;
                int num2 = ((i <= 1) ? 1 : (-1));
                Vector3D point = planet.GetClosestSurfacePointGlobal(targetPos + vector * num + vector2 * num2);
                if (!myOrientedBoundingBoxD.Contains(ref point))
                {
                    return false;
                }
            }
            return true;
        }
        private bool TestPlanetLandingPosition(MyPlanet planet, Vector3D landingPosition, float collisionRadius, bool testFreeZone, ref int distanceIteration)
        {
            if (testFreeZone && MinAirDensity > 0f && planet.GetAirDensity(landingPosition) < MinAirDensity)
            {
                return false;
            }

            Vector3D center = planet.PositionComp.WorldAABB.Center;
            Vector3D gravityVector = Vector3D.Normalize(landingPosition - center);
            Vector3D deviationNormal = MyUtils.GetRandomPerpendicularVector(ref gravityVector);
            if (!IsTerrainEven(planet, landingPosition, gravityVector, collisionRadius))
            {
                return false;
            }
            if (testFreeZone && !IsZoneFree(new BoundingSphereD(landingPosition, collisionRadius)))
            {
                distanceIteration++;
                return false;
            }
            return true;
        }
        private static bool IsZoneFree(BoundingSphereD safeZone)
        {
            ClearToken<MyEntity> clearToken = MyEntities.GetTopMostEntitiesInSphere(ref safeZone).GetClearToken();
            try
            {
                foreach (MyEntity item in clearToken.List)
                {
                    if (item is MyCubeGrid)
                    {
                        return false;
                    }
                }
            }
            finally
            {
                ((IDisposable)clearToken).Dispose();
            }
            return true;
        }


        public static BoundingBox GetSpawnShipBox(List<MyObjectBuilder_CubeGrid> spawningGrids)
        {
            BoundingBox prefabLocalBBox = BoundingBox.CreateInvalid();
            for (int k = 0; k < spawningGrids.Count - 1; k++)
            {
                BoundingBox box = spawningGrids[k].CalculateBoundingBox();
                prefabLocalBBox.Include(box);
            }

            return prefabLocalBBox;
        }



    }
}
