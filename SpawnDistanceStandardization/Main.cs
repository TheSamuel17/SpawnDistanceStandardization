using BepInEx;
using BepInEx.Configuration;
using RoR2;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace SpawnDistanceStandardization
{
    // Metadata
    [BepInPlugin("Samuel17.SpawnDistanceStandardization", "SpawnDistanceStandardization", "1.0.3")]

    public class Main : BaseUnityPlugin
    {
        // Master prefabs to be set to their respective distances.
        public static List<GameObject> masterPrefabsFar = new() {};
        public static List<GameObject> masterPrefabsStandard = new() {};
        public static List<GameObject> masterPrefabsClose = new() {};

        // Fields
        public static string defaultDistance = null;

        // Config fields
        public static ConfigEntry<string> farSpawns { get; private set; }
        public static ConfigEntry<string> standardSpawns { get; private set; }
        public static ConfigEntry<string> closeSpawns { get; private set; }

        public void Awake()
        {
            // Logging!
            Log.Init(Logger);

            // Load configs
            farSpawns = Config.Bind("Spawn Distances", "Far", "JellyfishMaster, AcidLarvaMaster, MagmaWormMaster, ElectricWormMaster", "Specify the monsters that should be set to Far (70-120m) by entering their internal master names.\nMake sure to separate them with commas.\nAlso accepts EverythingElse as a value.");
            standardSpawns = Config.Bind("Spawn Distances", "Standard", "BeetleMaster, ChildMaster, VerminMaster, ScorchlingMaster", "Specify the monsters that should be set to Standard (25-40m) by entering their internal master names.\nMake sure to separate them with commas.\nAlso accepts EverythingElse as a value.");
            closeSpawns = Config.Bind("Spawn Distances", "Close", "", "Specify the monsters that should be set to Close (8-20m) by entering their internal master names.\nMake sure to separate them with commas.\nAlso accepts EverythingElse as a value.");

            // Sort configs
            RoR2Application.onLoad += () =>
            {
                SortConfigs(farSpawns.Value, masterPrefabsFar, "Far");
                SortConfigs(standardSpawns.Value, masterPrefabsStandard, "Standard");
                SortConfigs(closeSpawns.Value, masterPrefabsClose, "Close");
            };    

            // Adjust spawn distances
            On.RoR2.ClassicStageInfo.RebuildCards += AdjustSpawnDistances;
        }

        private void SortConfigs(string spawns, List<GameObject> listPrefabs, string distance)
        {
            spawns = new string(spawns.ToCharArray().Where(c => !char.IsWhiteSpace(c)).ToArray());
            string[] monsters = spawns.Split(',');

            foreach (string str in monsters)
            {
                GameObject masterPrefab = MasterCatalog.FindMasterPrefab(str);
                if (masterPrefab)
                {
                    listPrefabs.Add(masterPrefab);
                    Log.Message(masterPrefab.name + " has been added to the " + distance + " spawn list.");
                } 
                else if (str == "EverythingElse")
                {
                    defaultDistance = distance;
                    Log.Message("Default spawn distance has been set to " + distance + ".");
                }
            }
        }

        private void AdjustSpawnDistances(On.RoR2.ClassicStageInfo.orig_RebuildCards orig, ClassicStageInfo self, DirectorCardCategorySelection forcedMonsterCategory = null, DirectorCardCategorySelection forcedInteractableCategory = null)
        {
            orig(self, forcedMonsterCategory, forcedInteractableCategory);

            // Skip if Void Fields, they're all set to spawn far by default
            if (SceneCatalog.mostRecentSceneDef != SceneCatalog.FindSceneDef("arena"))
            {
                if (self.monsterSelection == null) return;
                if (self.monsterSelection.choices == null) return;

                List<MasterCatalog.MasterIndex> list = new List<MasterCatalog.MasterIndex>(self.monsterSelection.choices.Length);
                for (int i = 0; i < self.monsterSelection.Count; i++)
                {
                    DirectorCard directorCard = self.monsterSelection.choices[i].value;
                    SpawnCard spawnCard = directorCard.GetSpawnCard();
                    if (spawnCard)
                    {
                        if (defaultDistance != null)
                        {
                            switch (defaultDistance)
                            {
                                case "Far":
                                    directorCard.spawnDistance = DirectorCore.MonsterSpawnDistance.Far;
                                    break;

                                case "Standard":
                                    directorCard.spawnDistance = DirectorCore.MonsterSpawnDistance.Standard;
                                    break;

                                case "Close":
                                    directorCard.spawnDistance = DirectorCore.MonsterSpawnDistance.Close;
                                    break;
                            }
                        }
                        
                        if (masterPrefabsFar.Contains(spawnCard.prefab)) // Set to "Far"
                        {
                            Log.Message(spawnCard.prefab.name + " spawn distance set to Far.");
                            directorCard.spawnDistance = DirectorCore.MonsterSpawnDistance.Far;
                        }
                        else if (masterPrefabsStandard.Contains(spawnCard.prefab)) // Set to "Standard"
                        {
                            Log.Message(spawnCard.prefab.name + " spawn distance set to Standard.");
                            directorCard.spawnDistance = DirectorCore.MonsterSpawnDistance.Standard;
                        }
                        else if (masterPrefabsClose.Contains(spawnCard.prefab)) // Set to "Close"
                        {
                            Log.Message(spawnCard.prefab.name + " spawn distance set to Close.");
                            directorCard.spawnDistance = DirectorCore.MonsterSpawnDistance.Close;
                        }
                    }
                }
            }
        }
    }
}
