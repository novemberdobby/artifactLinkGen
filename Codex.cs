using Newtonsoft.Json;
using System.Collections;
using System.Runtime.Serialization;
using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    class Codex : IEnumerable<Codex.Provider.Trait>, IDisposable
    {
        public enum IconLoadMode
        {
            None,
            Standard,
            Raw,
        }

        /// <summary>
        /// Empty/invalid slot, almost anything can be this
        /// </summary>
        public Provider.Trait EmptyBoon;

        public Dictionary<string, Provider.Trait> ByName { get; } = new();
        public Dictionary<string, List<Provider.Trait>> ByIcon { get; } = new();
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
            public string Name { get; set; }
            
            /// <summary>
            /// List of traits
            /// </summary>
            public List<Trait> Traits { get; set; }

            public Provider()
            {
                Name = string.Empty;
                Traits = new();
            }

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
            public sealed class Trait : IDisposable
            {
                [JsonRequired]
                public string Name { get; set; }

                /// <summary>
                /// Textual description of trait with no image data (i.e. "any X you collect is worth..." currently has no info on what X is)
                /// </summary>
                [JsonRequired]
                [JsonProperty("desc")]
                public string Description { get; set; }

                /// <summary>
                /// Path to icon file
                /// </summary>
                [JsonRequired]
                [JsonProperty("icon")]
                public string IconFile { get; set; }

                /// <summary>
                /// Icon data
                /// </summary>
                [JsonIgnore]
                public OCV.Mat? Icon { get; set; }

                /// <summary>
                /// See Subcategory enum
                /// </summary>
                [JsonProperty("singleton")]
                public Subcategory SingletonType { get; set; } = Subcategory.None;

                /// <summary>
                /// List of prerequisite traits from which all are required
                /// </summary>
                public List<string>? Requires { get; set; }

                /// <summary>
                /// List of prerequisite traits from which any will suffice
                /// </summary>
                [JsonProperty("requires_any")]
                public List<string>? RequiresAny { get; set; }

                /// <summary>
                /// List of traits which can't co-exist with this one
                /// </summary>
                [JsonProperty("incompatible_with")]
                public List<string>? IncompatibleWith { get; set; }

                /// <summary>
                /// List of providers, normally one but duo boons have two
                /// </summary>
                [JsonIgnore]
                public List<Provider> Providers { get; set; } = new();

                /// <summary>
                /// Trait category, if more than one provider exists they'll all be of the same category
                /// </summary>
                public Category Category => Providers.First().ProviderCategory;

                public Trait(string name, string description, string iconFile)
                {
                    Name = name;
                    Description = description;
                    IconFile = iconFile;
                }

                /// <summary>
                /// Read icon data, throws if it fails
                /// </summary>
                public void LoadIcon(IconLoadMode mode)
                {
                    if (mode == IconLoadMode.Raw)
                    {
                        Icon = OCV.Cv2.ImRead(IconFile, OCV.ImreadModes.Unchanged);
                    }
                    else
                    {
                        Icon = OCV.Cv2.ImRead(IconFile);
                    }

                    if (Icon == null || Icon.Empty())
                    {
                        throw new Exception($"Failed to load icon: {IconFile}");
                    }

                    //edit the icon so as to make it more easily comparable with others
                    if (NeedsPreprocess(Category))
                    {
                        var newIcon = CVUtil.MakeComparable(Icon);
                        Icon.Dispose();
                        Icon = newIcon;
                    }
                }

                public override string ToString()
                {
                    return Name;
                }

                public void Dispose()
                {
                    if (Icon != null)
                    {
                        Icon.Dispose();
                    }
                }
            }

            /// <summary>
            /// Resolve provider info
            /// </summary>
            [OnDeserialized]
            void OnDeserialized(StreamingContext context)
            {
                foreach (Trait item in Traits)
                {
                    item.Providers.Add(this);
                }
            }

            public override string ToString()
            {
                return $"{Name} ({Traits.Count} items)";
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
            EmptyBoon = this.First(t => t.Name == "boon");

            //once loaded, do a pass and resolve duo boons (by boon name which is unique)
            Dictionary<string, List<Provider.Trait>> boonsByName = new();
            foreach (Provider god in Providers.Where(p => p.ProviderCategory == Provider.Category.Gods))
            {
                foreach (var boon in god.Traits)
                {
                    if (boonsByName.TryGetValue(boon.Name, out List<Provider.Trait>? existing))
                    {
                        existing.Add(boon);
                    }
                    else
                    {
                        boonsByName.Add(boon.Name, new() { boon });
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

                /*
                //i want the names of everyone responsible
                string providerNames = string.Join("|", providers.Select(p => p.Name));
                Console.WriteLine($"Found duo ({providerNames}) {duo.Value.First()}");
                */
            }

            //only get images if requested
            if (iconMode != IconLoadMode.None)
            {
                Parallel.ForEach(this, item =>
                {
                    item.LoadIcon(iconMode);
                });
            }

            //map names to traits, map icon paths too so we know when one is used for several traits
            foreach (var trait in this)
            {
                ByName[trait.Name] = trait;

                string iconFile = trait.IconFile;
                if(!ByIcon.ContainsKey(iconFile))
                {
                    ByIcon.Add(iconFile, new());
                }

                ByIcon[iconFile].Add(trait);
            }

            //do a validity check on prerequisite lists
            foreach (var trait in this)
            {
                foreach (var prereqList in new[] { trait.Requires, trait.RequiresAny, trait.IncompatibleWith })
                {
                    if (prereqList != null)
                    {
                        var unknown = prereqList.Where(tr => !ByName.ContainsKey(tr));
                        if (unknown.Any())
                        {
                            throw new FormatException($"Found unknown trait(s) in prerequisites of {trait}: {string.Join(", ", unknown)}");
                        }
                    }
                }
            }

            //sort here so we can use [0] when we only need a representative for this group
            foreach (var sharers in ByIcon.Values)
            {
                sharers.Sort((l, r) => l.Name.CompareTo(r.Name));
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
            return new(JsonConvert.DeserializeObject<List<Provider>>(data), iconMode);
        }

        /// <summary>
        /// Deduce the weapon being used from a list of traits on a victory screen
        /// </summary>
        /// <param name="traits">Known traits</param>
        /// <returns>Weapon name</returns>
        /// <exception cref="Exception">Throws if we don't find exactly one weapon</exception>
        public static string? DetermineWeapon(IEnumerable<Provider.Trait> traits)
        {
            HashSet<string> weaponsByTrait = new();
            foreach (var trait in traits)
            {
                switch (trait.Category)
                {
                    case Provider.Category.Arm_Aspects:
                    case Provider.Category.Arm_Upgrades:
                        weaponsByTrait.Add(trait.Providers.First().Name);
                        break;

                    default:
                        break;
                }
            }

            if (weaponsByTrait.Count != 1)
            {
                Console.Error.WriteLine("Unable to determine active weapon from traits");
                return null;
            }

            return weaponsByTrait.First();
        }

        /// <summary>
        /// Find a list of all traits that share icons with this one
        /// </summary>
        /// <param name="traitName">Trait to find matches for</param>
        /// <returns>A list of traits including the one that was passed in</returns>
        public IEnumerable<Provider.Trait> GetIconSharingTraits(string traitName)
        {
            string iconFile = ByName[traitName].IconFile;
            return ByIcon[iconFile];
        }

        /// <summary>
        /// Does this look like an empty slot, or an invalid one (i.e. outside the tray)?
        /// </summary>
        public static bool IsSlotFilled(Provider.Trait? trait)
        {
            return trait != null && trait.Category != Provider.Category.Empty_Ability;
        }

        /// <summary>
        /// Compare trait equality by name; don't use if both instances of Duo boons are desired
        /// </summary>
        public class TraitEqualityComparer : IEqualityComparer<Provider.Trait>
        {
            public bool Equals(Provider.Trait? left, Provider.Trait? right)
            {
                if (ReferenceEquals(left, right))
                {
                    return true;
                }

                if(left == null || right == null)
                {
                    return false;
                }

                return left.Name == right.Name;
            }

            public int GetHashCode(Provider.Trait? trait)
            {
                return (trait?.Name == null ? 0 : trait.Name.GetHashCode());
            }
        }

        public IEnumerator<Provider.Trait> GetEnumerator()
        {
            foreach (Provider prov in Providers)
            {
                foreach (Provider.Trait item in prov.Traits)
                {
                    yield return item;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            foreach (Provider.Trait trait in this)
            {
                trait.Dispose();
            }
        }
    }
}
