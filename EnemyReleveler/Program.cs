
using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Newtonsoft.Json.Linq;
using Alphaleonis.Win32.Filesystem;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Newtonsoft.Json;
using Noggog;

namespace EnemyReleveler
{
    public enum LevelType
    {
        MaxLevel,
        MinLevel,
        Level
    }
    public class Program
    {
        public static List<String> underleveledNpcs = new List<String>();
        public static List<String> overleveledNpcs = new List<String>();
        public static List<String> lowPoweredNpcs = new List<String>();
        public static List<String> highPoweredNpcs = new List<String>();

        public static string[] npcsToIgnore = new string[]{
                "MQ101Bear",
                "WatchesTheRootsCorpse",
                "BreyaCorpse",
                "WatchesTheRoots",
                "Drennen",
                "Breya",
                "dunHunterBear",
                "DLC1HowlSummonWerewolf"
            };

        public static int[][] rule = new int[][]{
                    new int[] {0, 0},
                    new int[] {0, 0}
                };

        public static int Main(string[] args)
        {
            return SynthesisPipeline.Instance.Patch<ISkyrimMod, ISkyrimModGetter>(
                args,
                patcher: RunPatch,
                new UserPreferences
                {
                    ActionsForEmptyArgs = new RunDefaultPatcher
                    {
                        IdentifyingModKey = "enemies_releveled.esp",
                        TargetRelease = GameRelease.SkyrimSE
                    }
                });
        }
        public static void RunPatch(SynthesisState<ISkyrimMod, ISkyrimModGetter> state)
        {
            //set up rules
            var creatureRulesPath = Path.Combine(state.ExtraSettingsDataPath, "enemy_rules.json");

            if (!File.Exists(creatureRulesPath))
            {
                System.Console.Error.WriteLine($"ERROR: Missing required file {creatureRulesPath}");
            }

            Dictionary<string, int[][]> enemyRules = JsonConvert.DeserializeObject<Dictionary<string, int[][]>>(File.ReadAllText(creatureRulesPath));



            foreach (INpcGetter getter in state.LoadOrder.PriorityOrder.Npc().WinningOverrides())
            {
                //filter NPCs
                if (npcsToIgnore.Contains<String>(getter.EditorID ?? "") ||
                getter.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Stats)) continue;

                bool skip = true;

                foreach (var rank in getter.Factions)
                {
                    var faction = rank.Faction.Resolve(state.LinkCache)?.EditorID ?? "";
                    if (enemyRules.ContainsKey(faction))
                    {
                        skip = false;
                        rule = enemyRules[faction];
                    }
                    if (skip == false) break;
                }

                if (skip) continue;

                //Start releveling
                var npc = getter.DeepCopy();
                if (npc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.AutoCalcStats))
                {
                    EditValue(npc, LevelType.MinLevel, rule);
                    EditValue(npc, LevelType.MaxLevel, rule);
                }
                else
                {
                    EditValue(npc, LevelType.Level, rule);                    
                }
                state.PatchMod.Npcs.GetOrAddAsOverride(npc);
            }
            printWarnings();
        }


        public static void EditValue(INpc npc, LevelType levelType, int[][] rule)
        {
            short currentLevel = 1;
            switch (levelType)
            {
                case LevelType.MinLevel:
                    currentLevel = npc.Configuration.CalcMinLevel;
                    break;
                case LevelType.MaxLevel:
                    currentLevel = npc.Configuration.CalcMaxLevel;
                    if (currentLevel == 0) return;
                    break;
                case LevelType.Level:
                    if (npc.Configuration.Level is INpcLevelGetter level) currentLevel = level.Level;
                    if (currentLevel < rule[0][0]) underleveledNpcs.Add(npc.EditorID ?? "");
                    if (currentLevel > rule[0][1]) overleveledNpcs.Add(npc.EditorID ?? "");
                    break;
                default:
                    break;
            }
            int newLevel = ((currentLevel - rule[0][0]) / (rule[0][1] - rule[0][0])) * (rule[1][1] - rule[1][0]) + rule[1][0];

            if (newLevel < 1)
            {
                if (levelType == LevelType.Level) lowPoweredNpcs.Add(npc.EditorID ?? "");
                newLevel = 1;
            }
            if (newLevel > 100 & levelType == LevelType.Level) highPoweredNpcs.Add(npc.EditorID ?? "");

            switch (levelType)
            {
                case LevelType.MinLevel:
                    npc.Configuration.CalcMinLevel = (short)newLevel;
                    break;
                case LevelType.MaxLevel:
                    npc.Configuration.CalcMaxLevel = (short)newLevel;
                    break;
                case LevelType.Level:
                    npc.Configuration.Level = new NpcLevel()
                    {
                        Level = (short)newLevel
                    };
                    break;
                default:
                    break;
            }
        }

        public static void printWarnings()
        {
            if (underleveledNpcs.Count > 0)
            {
                Console.WriteLine("Warning, the following NPCs were at a lower level than the patcher expected (i.e. below the lower bound of the starting range). Its not a problem, and they have been patched, chances are another mod has changed their level too. This is just to let you know.");
                foreach (var item in underleveledNpcs)
                {
                    Console.WriteLine(item);
                }
            }
            if (overleveledNpcs.Count > 0)
            {
                Console.WriteLine("Warning, the following NPCs were at a higher level than the patcher expected (i.e. above the upper bound of the starting range). Its not a problem, and they have been patched, chances are another mod has changed their level too. This is just to let you know.");
                foreach (var item in overleveledNpcs)
                {
                    Console.WriteLine(item);
                }
            }
            if (lowPoweredNpcs.Count > 0)
            {
                Console.WriteLine("Warning, the faction rule told the patcher to give the following NPCs a level < 1. This has been ignored and the NPCs level has been set to 1.");
                foreach (var item in lowPoweredNpcs)
                {
                    Console.WriteLine(item);
                }
            }
            if (highPoweredNpcs.Count > 0)
            {
                Console.WriteLine("Warning, the faction rule told the patcher to give the following NPCs a level > 100. Good luck!");
                foreach (var item in highPoweredNpcs)
                {
                    Console.WriteLine(item);
                }
            }
        }
    }
}

