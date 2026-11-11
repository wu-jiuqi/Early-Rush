using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V8
{
    /// <summary>Unmodified local art-v4 PNGs. Original V8Art resources always win.
    /// Aliases describe semantic equivalents, not a new animation set.</summary>
    public static class RushLocalArtV14
    {
        static readonly Dictionary<string, string> aliases = new Dictionary<string, string>
        {
            {"people/boy-up", "characters/player-back"},
            {"people/boy-down", "characters/player-front"},
            {"people/boy-left", "characters/player-left"},
            {"people/boy-right", "characters/player-right"},
            {"bike-feedback/bike-left", "characters/player-bike-left"},
            {"bike-feedback/bike-right", "characters/player-bike-right"},
            // No vertical bike images exist. These are explicitly lateral stills.
            {"bike-feedback/bike-up", "characters/player-bike-right"},
            {"bike-feedback/bike-down", "characters/player-bike-right"},
            {"floors-v3/asphalt", "materials/asphalt"},
            {"floors-v3/carpet", "materials/office-carpet"},
            {"floors-v3/ceramic", "materials/cream-tile"},
            {"legacy-floors/floor-tile", "materials/cream-tile"},
            {"tasks/chair", "office/office-chair"},
            {"tasks/cloth", "metro/folded-cloth"},
            {"tasks/wheel", "street/loose-wheel"},
            {"street/bench", "environment/street-bench"},
            {"street/bush-planter", "environment/tree-planter"},
            {"street/long-planter", "environment/flower-planter"},
            {"street/storefront", "environment/street-building"},
            {"street/station-doorway", "street/metro-entrance"},
            {"stalls/breakfast-stall", "street/breakfast-stall"},
            {"metro/platform-bench", "metro/station-bench"},
            {"metro/pillar", "metro/station-column"},
            {"metro/vending", "metro/capsule-machine"},
            {"metro/tactile-strip", "metro/train-platform-strip"},
            {"metro/train-open", "metro/train-door-module"},
            {"metro/water-patch", "metro/water-patch"},
            {"metro/exit-marker", "icons/destination-exit"},
            {"office/desk-chair", "office/empty-workstation"},
            {"office/plant", "environment/office-plant"},
            {"office/filing-cabinet", "office/file-pickup-counter"},
            {"office/reception", "environment/reception-desk"},
            {"office/meeting-table", "office/meeting-table"},
            {"legacy-extras/breakfast", "icons/task-breakfast"},
            {"legacy-extras/coffee", "icons/task-coffee"},
            {"legacy-icons/bike", "street/reward-bicycle"},
            {"legacy-icons/exit", "icons/destination-exit"},
            {"legacy-icons/train", "icons/destination-metro"},
        };
        static readonly HashSet<string> procedural = new HashSet<string>
        {
            "ui/selection", "ui/teal-hover", "ui/teal-normal", "ui/speech", "ui/thought",
            "fx-decal/actor-shadow", "fx-decal/arrows", "fx-decal/ripple", "fx-decal/rain",
            "legacy-icons/clock", "legacy-icons/pause", "legacy-icons/play", "legacy-icons/retry",
            "legacy-icons/rain", "legacy-extras/check", "legacy-extras/star", "office/clock-mat",
            "street/curb-horizontal", "corrections/crosswalk-flat"
        };
        public static bool IsProcedural(string key) { return procedural.Contains(key); }
        public static bool TryPath(string key, out string path)
        {
            if (aliases.TryGetValue(key, out path)) return true;
            if(key.StartsWith("people/source-")){string name=key.Substring(14);foreach(string suffix in new[]{"-up","-down","-left","-right"})if(name.EndsWith(suffix)){name=name.Substring(0,name.Length-suffix.Length);break;}path="npcs/"+name;return true;}
            // The supplied NPC views depict different outfits/identities. Keep a consistent
            // individual across movement, rather than claiming those are four-way frames.
            if (key.StartsWith("people/coffee-female-")) path = "office/coffee-colleague-female";
            else if (key.StartsWith("people/coffee-male-")) path = "office/coffee-colleague-male";
            else if (key.StartsWith("people/man-") || key.StartsWith("people/commuter-"))
                path = "npcs/commuter-male";
            else if (key.StartsWith("people/woman-")) path = "npcs/commuter-female";
            else return false;
            return true;
        }
        public static Texture2D Load(string key)
        {
            // Optional verified generated fills. Missing files retain the honest placeholder log.
            string generated = key == "metro/train-closed" ? "train-closed"
                : key == "office/sofa" ? "sofa"
                : key == "metro/escalator" ? "escalator"
                : key == "metro/kiosk" ? "kiosk" : null;
            if (generated != null)
            {
                var fill = Resources.Load<Texture2D>("V14Generated/" + generated);
                if (fill)
                {
                    Debug.Log("EARLY_RUSH_GENERATED_ART " + key + " -> V14Generated/" + generated);
                    return fill;
                }
            }
            if (!TryPath(key, out var path)) return null;
            var texture = Resources.Load<Texture2D>("V8ArtV4/" + path);
            if (texture) Debug.Log("EARLY_RUSH_LOCAL_ART " + key + " -> V8ArtV4/" + path);
            return texture;
        }
    }
}
