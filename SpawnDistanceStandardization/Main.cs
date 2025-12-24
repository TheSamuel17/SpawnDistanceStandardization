using BepInEx;
using BepInEx.Configuration;
using RoR2;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;

namespace SpawnDistanceStandardization
{
    // Metadata
    [BepInPlugin("Samuel17.SpawnDistanceStandardization", "SpawnDistanceStandardization", "1.0.4")]

    public class Main : BaseUnityPlugin
    {
        // Lists pertaining to spawn distance
        public static List<GameObject> masterPrefabsFar = new() {};
        public static List<GameObject> masterPrefabsStandard = new() {};
        public static List<GameObject> masterPrefabsClose = new() {};

        // Lists pertaining to minimum stages
        public static List<GameObject> minStagesMasterPrefabs = new() {};
        public static List<int> minStagesCounts = new() {};

        // Fields
        public static string defaultDistance = null;
        public static bool noStageCountRestrictions = false;
        public static bool isFamilyEventActive = false;

        // Config fields
        public static ConfigEntry<string> farSpawns { get; private set; }
        public static ConfigEntry<string> standardSpawns { get; private set; }
        public static ConfigEntry<string> closeSpawns { get; private set; }
        public static ConfigEntry<string> minStages { get; private set; }

        public void Awake()
        {
            // Logging!
            Log.Init(Logger);

            // Load configs
            farSpawns = Config.Bind("Spawn Distances", "Far", "JellyfishMaster, AcidLarvaMaster, MagmaWormMaster, ElectricWormMaster", "Specify the monsters that should be set to Far (70-120m) by entering their internal master names.\nMake sure to separate them with commas.\nAlso accepts EverythingElse as a value.");
            standardSpawns = Config.Bind("Spawn Distances", "Standard", "BeetleMaster, ChildMaster, VerminMaster, ScorchlingMaster", "Specify the monsters that should be set to Standard (25-40m) by entering their internal master names.\nMake sure to separate them with commas.\nAlso accepts EverythingElse as a value.");
            closeSpawns = Config.Bind("Spawn Distances", "Close", "", "Specify the monsters that should be set to Close (8-20m) by entering their internal master names.\nMake sure to separate them with commas.\nAlso accepts EverythingElse as a value.");
            minStages = Config.Bind("Miscellaneous", "Minimum Stages", "", "Specify a monster's master name, followed by the lowest stage number it's allowed to appear in. Example: 'FlyingVerminMaster - 2' will make Blind Pests spawn on Stage 2 onwards.\nMake sure to separate the different entries with commas.\nAlternatively, enter 'NoRestrictions' to remove stage count restrictions for every monster.");

            // Sort configs
            RoR2Application.onLoad += () =>
            {
                SortDistConfigs(farSpawns.Value, masterPrefabsFar, "Far");
                SortDistConfigs(standardSpawns.Value, masterPrefabsStandard, "Standard");
                SortDistConfigs(closeSpawns.Value, masterPrefabsClose, "Close");
                SortMinStageConfigs(minStages.Value);

                // Adjust spawn distances
                if (defaultDistance != null || masterPrefabsFar.Count > 0 || masterPrefabsStandard.Count > 0 || masterPrefabsClose.Count > 0)
                {
                    On.RoR2.ClassicStageInfo.RebuildCards += AdjustSpawnDistances;
                }

                // Adjust minimum stages - hate the implementation but the alternative is sniping every stage monster DCCS. Not doing that.
                if (minStagesMasterPrefabs.Count > 0 || noStageCountRestrictions == true)
                {
                    On.RoR2.DirectorCard.IsAvailable += AdjustMinimumStage;

                    // Hacky way of tracking family events
                    On.RoR2.ClassicStageInfo.BroadcastFamilySelection += TrackFamilyEvent;
                    On.RoR2.ClassicStageInfo.Start += ResetFamilyEvent;
                }
            };    
        }

        private void SortDistConfigs(string spawns, List<GameObject> listPrefabs, string distance)
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

        private void SortMinStageConfigs(string spawns)
        {
            spawns = new string(spawns.ToCharArray().Where(c => !char.IsWhiteSpace(c)).ToArray());
            string[] entries = spawns.Split(',');

            foreach (string str in entries)
            {
                if (str == "NoRestrictions")
                {
                    noStageCountRestrictions = true;
                    Log.Message("Cleared minimum stage restrictions for every monster.");
                }
                else
                {
                    string[] splitEntries = str.Split('-');

                    if (splitEntries.Length == 2) // Make sure the "MonsterMaster - StageCount" template is respected
                    {
                        if (int.TryParse(splitEntries[1], out int stageCount))
                        {
                            GameObject masterPrefab = MasterCatalog.FindMasterPrefab(splitEntries[0]);
                            if (masterPrefab)
                            {
                                minStagesMasterPrefabs.Add(masterPrefab);
                                minStagesCounts.Add(stageCount);
                                Log.Message(masterPrefab.name + " minimum stage set to " + stageCount + ".");
                            }
                        }
                    }
                }
            }
        }

        private void AdjustSpawnDistances(On.RoR2.ClassicStageInfo.orig_RebuildCards orig, ClassicStageInfo self, DirectorCardCategorySelection forcedMonsterCategory = null, DirectorCardCategorySelection forcedInteractableCategory = null)
        {
            orig(self, forcedMonsterCategory, forcedInteractableCategory);

            // Skip if Void Fields, they're all set to spawn far by default and only the Scav has a stage count restriction
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
                            directorCard.spawnDistance = DirectorCore.MonsterSpawnDistance.Far;
                            Log.Message(spawnCard.prefab.name + " spawn distance set to Far.");
                        }
                        else if (masterPrefabsStandard.Contains(spawnCard.prefab)) // Set to "Standard"
                        {  
                            directorCard.spawnDistance = DirectorCore.MonsterSpawnDistance.Standard;
                            Log.Message(spawnCard.prefab.name + " spawn distance set to Standard.");
                        }
                        else if (masterPrefabsClose.Contains(spawnCard.prefab)) // Set to "Close"
                        {
                            directorCard.spawnDistance = DirectorCore.MonsterSpawnDistance.Close;
                            Log.Message(spawnCard.prefab.name + " spawn distance set to Close.");
                        }
                    }
                }
            }
        }

        private bool AdjustMinimumStage(On.RoR2.DirectorCard.orig_IsAvailable orig, DirectorCard self)
        {
            bool flag2 = RunArtifactManager.instance?.IsArtifactEnabled(RoR2Content.Artifacts.mixEnemyArtifactDef) ?? false;
            if (!flag2 && isFamilyEventActive == false) // Ignore if Dissonance or a Family Event is active
            {
                // Skip if Void Fields, only the Scav has a stage count restriction there
                if (SceneCatalog.mostRecentSceneDef != SceneCatalog.FindSceneDef("arena") || noStageCountRestrictions == true)
                {
                    SpawnCard spawnCard = self.GetSpawnCard();
                    if (spawnCard)
                    {
                        if (minStagesMasterPrefabs.Contains(spawnCard.prefab))
                        {
                            int index = minStagesMasterPrefabs.IndexOf(spawnCard.prefab);
                            self.minimumStageCompletions = minStagesCounts[index] - 1;
                        }
                        else if (noStageCountRestrictions == true)
                        {
                            GameObject masterPrefab = MasterCatalog.FindMasterPrefab(spawnCard.prefab.name);
                            if (masterPrefab)
                            {
                                self.minimumStageCompletions = 0;
                            }
                        }
                    }
                }
            }

            return orig(self);
        }

        private IEnumerator TrackFamilyEvent(On.RoR2.ClassicStageInfo.orig_BroadcastFamilySelection orig, ClassicStageInfo self, string familySelectionChatString)
        {
            isFamilyEventActive = true;
            return orig(self, familySelectionChatString);
        }

        private void ResetFamilyEvent(On.RoR2.ClassicStageInfo.orig_Start orig, ClassicStageInfo self)
        {
            isFamilyEventActive = false;
            orig(self);
        }
    }
}
