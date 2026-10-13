using System.Collections.Generic;
using UnityEngine;

namespace BiomechanicalCrafting
{
    public static class BlueprintRecipeRules
    {
        private static readonly string[][][] Recipes =
        {
            new[] { new[] { "mat_elec_clam_shell", "mat_elec_muscle_bundle" } },
            Pairs(new[] { "mat_wood_coral_plate" }, new[] { "mat_wood_hollow_coral", "mat_wood_root_gear", "mat_wood_symbiotic_fungi", "mat_wood_calcified_valve", "mat_wood_rubber_seal", "mat_wood_titanium_patch", "mat_wood_spring_fascia" }, new[] { new[] { "mat_wood_hollow_coral", "mat_wood_root_gear" }, new[] { "mat_wood_hollow_coral", "mat_wood_symbiotic_fungi" } }),
            new[] { new[] { "mat_gas_bubble_sac", "mat_gas_float_bladder" } },
            Pairs(new[] { "mat_water_ventricle" }, new[] { "mat_water_sponge_filter", "mat_water_siphon_tube", "mat_water_viscous_gel", "mat_water_algae_filament", "mat_water_keratin_nozzle", "mat_water_patrol_lens", "mat_water_neon_powder" }, new[] { new[] { "mat_water_sponge_filter", "mat_water_siphon_tube" } }),
            Pairs(new[] { "mat_water_logic_chip" }, new[] { "mat_elec_piezo_chip", "mat_water_patrol_lens", "mat_wood_symbiotic_fungi" }),
            new[] { new[] { "mat_water_keratin_nozzle", "mat_gas_valve_pipe" } },
            Pairs(new[] { "mat_wood_coral_plate", "mat_wood_hollow_coral" }, new[] { "mat_gas_valve_pipe", "mat_gas_vortex_cartilage", "mat_water_logic_chip" })
        };
        public static readonly string[] ProductIds = { "furn_eel_generator", "furn_coral_wall", "furn_pneumatic_bed", "furn_water_tower", "furn_bio_incubator", "furn_hydro_cannon", "furn_gather_door" };

        private static string[][] Pairs(string[] cores, string[] accessories, string[][] alternatives = null)
        {
            var pairs = new List<string[]>();
            foreach (var core in cores)
                foreach (var accessory in accessories) pairs.Add(new[] { core, accessory });
            if (alternatives != null) pairs.AddRange(alternatives);
            return pairs.ToArray();
        }

        public static string[] GetPreferredIngredients(string productId, HashSet<string> heldIds)
        {
            int index = System.Array.IndexOf(ProductIds, productId);
            if (index < 0) return new string[0];
            string[] best = Recipes[index][0];
            int bestCount = -1;
            foreach (var pair in Recipes[index])
            {
                int count = (heldIds.Contains(pair[0]) ? 1 : 0) + (heldIds.Contains(pair[1]) ? 1 : 0);
                if (count > bestCount) { bestCount = count; best = pair; }
            }
            return (string[])best.Clone();
        }

        public static HashSet<string> GetIngredientIds(string productId)
        {
            var ids = new HashSet<string>();
            int index = System.Array.IndexOf(ProductIds, productId);
            if (index >= 0) foreach (var pair in Recipes[index]) foreach (var id in pair) ids.Add(id);
            return ids;
        }

        public static string Match(IEnumerable<BiomechanicalMaterial> materials)
        {
            var ids = new HashSet<string>();
            foreach (var material in materials) if (material != null) ids.Add(material.id);
            for (int recipeIndex = 0; recipeIndex < Recipes.Length; recipeIndex++)
                foreach (var pair in Recipes[recipeIndex])
                    if (ids.Contains(pair[0]) && ids.Contains(pair[1])) return ProductIds[recipeIndex];
            return null;
        }

        public static bool AreAdjacent(BiomechanicalMaterial first, BiomechanicalMaterial second, ShellConfig config)
        {
            if (first == null || second == null || ReferenceEquals(first, second) || config == null) return false;
            var firstSlots = first.occupiedSlotIds != null && first.occupiedSlotIds.Count > 0 ? first.occupiedSlotIds : new List<int> { first.rootSlotId };
            var secondSlots = second.occupiedSlotIds != null && second.occupiedSlotIds.Count > 0 ? second.occupiedSlotIds : new List<int> { second.rootSlotId };
            foreach (int firstId in firstSlots)
                foreach (int secondId in secondSlots)
                    if (firstId >= 0 && firstId < config.slots.Count && secondId >= 0 && secondId < config.slots.Count && config.slots[firstId].IsAdjacent8Way(config.slots[secondId])) return true;
            return false;
        }

        public static bool IsValidStroke(List<BiomechanicalMaterial> path, ShellConfig config)
        {
            if (path == null || path.Count < 2) return false;
            var unique = new HashSet<BiomechanicalMaterial>();
            for (int index = 0; index < path.Count; index++)
                if (path[index] == null || !unique.Add(path[index]) || (index > 0 && !AreAdjacent(path[index - 1], path[index], config))) return false;
            return true;
        }

        public static bool TryFindPath(string productId, List<BiomechanicalMaterial> materials, ShellConfig config, out List<BiomechanicalMaterial> path)
        {
            path = new List<BiomechanicalMaterial>();
            int targetIndex = System.Array.IndexOf(ProductIds, productId);
            if (targetIndex < 0 || materials == null || config == null) return false;
            var nodes = new List<BiomechanicalMaterial>();
            foreach (var material in materials) if (material != null && !nodes.Contains(material)) nodes.Add(material);
            foreach (var pair in Recipes[targetIndex])
            {
                if (!nodes.Exists(node => node.id == pair[0]) || !nodes.Exists(node => node.id == pair[1])) continue;
                if (FindCompatiblePath(nodes, config, targetIndex, pair, new HashSet<string>(), new HashSet<string>(), out path)) return true;
            }
            path = new List<BiomechanicalMaterial>();
            return false;
        }

        private static bool FindCompatiblePath(List<BiomechanicalMaterial> nodes, ShellConfig config, int target, string[] required, HashSet<string> excluded, HashSet<string> visited, out List<BiomechanicalMaterial> path)
        {
            path = new List<BiomechanicalMaterial>();
            var exclusions = new List<string>(excluded);
            exclusions.Sort(System.StringComparer.Ordinal);
            if (!visited.Add(string.Join("|", exclusions))) return false;
            var previous = new Dictionary<BiomechanicalMaterial, BiomechanicalMaterial>();
            var queue = new Queue<BiomechanicalMaterial>();
            foreach (var node in nodes)
                if (node.id == required[0] && !excluded.Contains(node.id)) { previous[node] = null; queue.Enqueue(node); }
            BiomechanicalMaterial destination = null;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.id == required[1]) { destination = current; break; }
                foreach (var next in nodes)
                    if (!excluded.Contains(next.id) && !previous.ContainsKey(next) && AreAdjacent(current, next, config)) { previous[next] = current; queue.Enqueue(next); }
            }
            if (destination == null) return false;
            for (var node = destination; node != null; node = previous[node]) path.Add(node);
            path.Reverse();
            if (Match(path) == ProductIds[target]) return true;
            var ids = new HashSet<string>();
            foreach (var node in path) ids.Add(node.id);
            for (int recipe = 0; recipe < target; recipe++)
                foreach (var conflict in Recipes[recipe])
                {
                    if (!ids.Contains(conflict[0]) || !ids.Contains(conflict[1])) continue;
                    foreach (var excludedId in conflict)
                    {
                        if (excludedId == required[0] || excludedId == required[1]) continue;
                        excluded.Add(excludedId);
                        if (FindCompatiblePath(nodes, config, target, required, excluded, visited, out path)) return true;
                        excluded.Remove(excludedId);
                    }
                    path.Clear();
                    return false;
                }
            path.Clear();
            return false;
        }
    }
}
