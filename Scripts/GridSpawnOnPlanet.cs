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
        private float SpawnHeight = 0; //Leave 0 to spawn as close to the surface as possible
        private float ShipCollisionRadius = 20;
        private float MinAirDensity = 0f;


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

            Log.Info("A");

            if (!RegionHandler.TryGetServer(RegionHandler.ThisServer.ServerID, out TargetServer))
                return;

            Log.Info("B");
            List<MyPlanet> allOptions = GetTargetPlanets(TargetServer);
            if(allOptions.Count <= 0)
                return;

            Log.Info("C");
            BoundingBox box = GetSpawnShipBox(spawnReqMsg.SpawningGrids);
            for (int i = 0; i < 30; i++)
            {
                //Shuffle option
                allOptions.ShuffleList();
                //Get first index after shuffle
                MyPlanet pTarget = allOptions[0];

                Log.Info($"Attempting to spawn grids @ {pTarget.DisplayName}. Iteration: {i}");
                Vector3D position = await AsyncInvoke.InvokeAsync(() => TryGetPlanetSpawn(pTarget, TargetServer, box.HalfExtents.Length()));

                //Found Valid position
                if(position != null && position != Vector3D.Zero)
                {
                    spawnReqMsg.GridSpawnPosition = position;
                    return;
                }
            }

            /* Separate logic here to check if its null or no options found */
            
           


            return;
        }

        public BoundingSphereD GetMaxSearchArea()
        {
            BoundingSphereD Search = new BoundingSphereD();
            List<SectorAPI> allSectors = TargetServer.ChildSectors;
            if (allSectors.Count != 0)
            {
                int randInt = rand.Next(allSectors.Count);
                TargetSector = allSectors[randInt];



                Search = TargetSector.GetMaxBoundingSphereInside();
            }

            if (Search == null || Search.Radius == 0)
                Search = new BoundingSphereD(Vector3D.Zero, 200000);

            return Search;
        }


        public List<MyPlanet> GetTargetPlanets(ServerAPI TargetServer)
        {
            // Attempt to get target planet from name
            List<MyPlanet> myPlanets = new List<MyPlanet>();

            if (!string.IsNullOrEmpty(TargetPlanet))
            {
                myPlanets = MyPlanets.GetPlanets().Where(x => x.DisplayName.Contains(TargetPlanet)).ToList();

                if (myPlanets == null || myPlanets.Count == 0)
                    Log.Fatal($"Failed to find a planet with the name of: \"{TargetPlanet}\". One will be provided automatically...");

                return myPlanets;
            }
            

            // Check to see if the target server even has sectors
            if (TargetServer.HasSectors)
            {

                //Search all planets and see if any are inside of our servers sectors
                foreach (MyPlanet ent in MyPlanets.GetPlanets())
                {
                    SphereSector PlanetVolume = new SphereSector(ent.PositionComp.GetPosition(), ent.MaximumRadius);
                    if (TargetServer.ChildSectors.Any(x => x.Contains(PlanetVolume) == ContainmentType.Disjoint))
                        continue;

                    myPlanets.Add(ent);
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

            float optimalSpawnDistance = MySession.Static.Settings.OptimalSpawnDistance;
            float minimalClearance = (optimalSpawnDistance - optimalSpawnDistance * 0.5f) * 0.9f;

            Vector3D globalPos = Vector3D.Zero;

            for (int i = 0; i < 35; i++)
            {
                Vector3D randomPerpendicularVector = MyUtils.GetRandomVector3D();
                float num = optimalSpawnDistance * (MyUtils.GetRandomFloat(0.54999995f, 1.65f) + (float)distanceIteration * 0.05f);
                globalPos = randomPerpendicularVector * num;
                globalPos = planet.GetClosestSurfacePointGlobal(ref globalPos);


                /* Check to see if position is actually in our server */
                if (targetServer.HasSectors)
                {
                    RegionHandler.TryGetSmallestSector(globalPos, out var sector);
                    if (sector.Data.OnServerID != targetServer.ServerID)
                        continue;
                }


                if (!TestPlanetLandingPosition(planet, globalPos, true, minimalClearance, collisionRadius, ref distanceIteration))
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
        private bool TestPlanetLandingPosition(MyPlanet planet, Vector3D landingPosition, bool testFreeZone, float minimalClearance, float collisionRadius, ref int distanceIteration)
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
            if (testFreeZone && !IsZoneFree(new BoundingSphereD(landingPosition, minimalClearance)))
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
