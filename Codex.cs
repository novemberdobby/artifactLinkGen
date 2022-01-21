using Newtonsoft.Json;
using System.Collections;
using System.Runtime.Serialization;
using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    class Codex : IEnumerable<Codex.Provider.Equippable>
    {
        public enum IconLoadMode
        {
            None,
            Standard,
            Raw,
        }

        public List<Provider> Providers { get; set; }

        public class Provider
        {
            /// <summary>
            /// See Category enum
            /// </summary>
            [JsonProperty("category")]
            public Category ProviderCategory { get; set; }

            /// <summary>
            /// Provider pool/name, e.g. a god, weapon, etc
            /// </summary>
            public string? Name { get; set; }
            
            /// <summary>
            /// List of traits
            /// </summary>
            public List<Equippable>? Equips { get; set; }

            /// <summary>
            /// Various types of trait
            /// </summary>
            public enum Category
            {
                /// <summary>
                /// Boons
                /// </summary>
                Gods,

                /// <summary>
                /// Infernal arm upgrades
                /// </summary>
                Arm_Upgrades,

                /// <summary>
                /// Infernal arm aspects
                /// </summary>
                Arm_Aspects,

                /// <summary>
                /// Standard keepsakes e.g. Old Spiked Collar (Cerberus)
                /// </summary>
                Keepsakes,

                /// <summary>
                /// Legendary keepsakes e.g. Companion Battie (Meg)
                /// </summary>
                Companions,

                /// <summary>
                /// Empty slot for any trait (including Subcategory items - see below)
                /// </summary>
                Empty_Ability,

                /// <summary>
                /// Temporary items purchased from the Well of Charon
                /// </summary>
                Charons_Well,

                /// <summary>
                /// Unique traits e.g. bouldy blessings, mirror skills that provide a trait, membership card
                /// </summary>
                Special_Item,

                /// <summary>
                /// One of Eurydice's upgrades
                /// </summary>
                Eurydice,
            }

            /// <summary>
            /// Each god provides a single boon that can fit into one of the slots below, and players can only hold one of each at a time (or that slot can be empty)
            /// </summary>
            public enum Subcategory
            {
                None,

                Attack,
                Call,
                Cast,
                Dash,
                Special,
            }

            /// <summary>
            /// Check whether we need to preprocess icons for certain types of items which aren't diamond-shaped
            /// </summary>
            /// <param name="providerType">Trait's provider category</param>
            public static bool NeedsPreprocess(Category providerType)
            {
                return providerType switch
                {
                    Category.Companions or Category.Keepsakes => false,
                    _ => true,
                };
            }

            /// <summary>
            /// Main trait class
            /// </summary>
            public class Equippable
            {
                public string? Name { get; set; }

                /// <summary>
                /// Textual description of trait with no image data (i.e. "any X you collect is worth..." currently has no info on what X is)
                /// </summary>
                [JsonRequired]
                [JsonProperty("desc")]
                public string? Description { get; set; }

                /// <summary>
                /// Path to icon file
                /// </summary>
                [JsonRequired]
                [JsonProperty("icon")]
                public string? IconFile { get; set; }

                /// <summary>
                /// See Subcategory enum
                /// </summary>
                [JsonProperty("singleton")]
                public Subcategory SingletonType { get; set; } = Subcategory.None;

                /// <summary>
                /// Icon data
                /// </summary>
                [JsonIgnore]
                public OCV.Mat? Icon { get; set; }

                /// <summary>
                /// List of providers, normally one but duo boons have two
                /// </summary>
                [JsonIgnore]
                public List<Provider> Providers { get; set; } = new();

                /// <summary>
                /// Trait category, if more than one provider exists they'll all be of the same category
                /// </summary>
                public Category Category => Providers.First().ProviderCategory;
                
                /// <summary>
                /// Read icon data, throws if it fails
                /// </summary>
                public void LoadIcon(IconLoadMode mode)
                {
                    if(mode == IconLoadMode.Raw)
                    {
                        Icon = OCV.Cv2.ImRead(IconFile!, OCV.ImreadModes.Unchanged)!;
                    }
                    else
                    {
                        Icon = OCV.Cv2.ImRead(IconFile!)!;
                    }
                }

                public override string ToString()
                {
                    return Name!;
                }

                /// <summary>
                /// Edit the trait icon so as to make it more easily comparable with others
                /// </summary>
                public void MakeComparable()
                {
                    if(NeedsPreprocess(Providers.First().ProviderCategory))
                    {
                        Icon = CVUtil.MakeComparable(Icon!);
                    }
                }
            }

            /// <summary>
            /// Resolve provider info
            /// </summary>
            [OnDeserialized]
            void OnDeserialized(StreamingContext context)
            {
                foreach (Equippable item in Equips!)
                {
                    item.Providers.Add(this);
                }
            }

            public override string ToString()
            {
                return $"{Name} ({Equips!.Count} items)";
            }
        }

        /// <summary>
        /// Create a new codex
        /// </summary>
        /// <param name="provs">Deserialised provider data</param>
        /// <param name="loadIcons">Whether to load icon data</param>
        private Codex(List<Provider> provs, IconLoadMode iconMode)
        {
            Providers = provs;

            //once loaded, do a pass and resolve duo boons (by boon name which is unique)
            Dictionary<string, List<Provider.Equippable>> boonsByName = new();
            foreach (Provider god in Providers.Where(p => p.ProviderCategory == Provider.Category.Gods))
            {
                foreach (var boon in god.Equips!)
                {
                    if (boonsByName.TryGetValue(boon.Name!, out List<Provider.Equippable>? existing))
                    {
                        existing.Add(boon);
                    }
                    else
                    {
                        boonsByName.Add(boon.Name!, new() { boon });
                    }
                }
            }

            foreach (var duo in boonsByName.Where(b => b.Value.Count > 1))
            {
                List<Provider> providers = new(duo.Value.SelectMany(d => d.Providers));
                foreach (var boon in duo.Value)
                {
                    boon.Providers = providers;
                }

#if DEBUG
                //i want the names of everyone responsible
                string providerNames = string.Join("|", providers.Select(p => p.Name));
                Console.WriteLine($"Found duo ({providerNames}) {duo.Value.First()}");
#endif
            }

            //only get images if requested
            if (iconMode != IconLoadMode.None)
            {
                foreach (Provider.Equippable item in this.Distinct())
                {
                    item.LoadIcon(iconMode);
                    item.MakeComparable();
                }
            }
        }

        /// <summary>
        /// Load codex data
        /// </summary>
        /// <param name="inputFile"></param>
        /// <returns></returns>
        public static Codex FromFile(string inputFile, IconLoadMode iconMode)
        {
            string data = File.ReadAllText(inputFile);
            return new(JsonConvert.DeserializeObject<List<Provider>>(data), loadIcons);
        }

        public IEnumerator<Provider.Equippable> GetEnumerator()
        {
            foreach (Provider prov in Providers)
            {
                foreach (Provider.Equippable item in prov.Equips!)
                {
                    yield return item;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
