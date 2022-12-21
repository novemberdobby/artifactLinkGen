namespace HadesBoonBot.Classifiers
{
    internal class ClassifiedScreenMeta
    {
        public readonly ClassifiedScreen? Screen;
        public readonly string? RemoteSource;
        public readonly string? LocalSource;

        public ClassifiedScreenMeta(ClassifiedScreen? screen, string? remoteSource, string? localSource)
        {
            Screen = screen;
            RemoteSource = remoteSource;
            LocalSource = localSource;
        }
    }

    internal class ClassifiedScreen
    {
        public string? WeaponName;
        public List<Slot> Slots;
        public List<Slot> PinSlots; //store pin slots separately so as not to duplicate tray data
        public bool IsValid { get; private set; }

        public class Slot
        {
            public Codex.Provider.Trait Trait;
            public int Col;
            public int Row;

            public Slot(Codex.Provider.Trait trait, int col, int row)
            {
                Trait = trait;
                Col = col;
                Row = row;
            }

            public override string ToString()
            {
                if (Col != -1)
                {
                    return $"Tray #{Col}_{Row}: {Trait}";
                }

                return $"Pinned #{Row}: {Trait}";
            }
        }

        /// <summary>
        /// Determine number of trait tray columns according to tray slot list
        /// </summary>
        /// <param name="includeEmpties">Include empty ('boon') slots. Depends what the caller needs this number for.</param>
        /// <returns>Number of trait tray columns</returns>
        public int CalculateColumnCount(bool includeEmpties)
        {
            int slotsToTake = Slots.Count - (includeEmpties ? 0 : GetEmptyBoonEndCount());
            int lastColumn = Slots.Take(slotsToTake).Max(x => x.Col);
            return lastColumn + 1;
        }

        /// <summary>
        /// Count the number of continuous empty slots at the end of the trait tray (which is filled out by row then column)
        /// </summary>
        /// <returns>Number of slots at the end of `Slots` that are empty</returns>
        public int GetEmptyBoonEndCount()
        {
            return Slots
                .AsEnumerable()
                .Reverse()
                .TakeWhile(s => !Codex.IsSlotFilled(s.Trait))
                .Count();
        }

        /// <summary>
        /// Construct a classification result from a list of trait slots, do some cleanup etc
        /// </summary>
        /// <param name="codex">Trait list</param>
        /// <param name="inSlots">Classified traits</param>
        public ClassifiedScreen(Codex codex, IEnumerable<Slot> inSlots)
        {
            //split into tray & pins
            Slots = inSlots.Where(s => s.Col != -1).ToList();
            PinSlots = inSlots.Where(s => s.Col == -1).ToList();

            WeaponName = Codex.DetermineWeapon(Slots.Select(s => s.Trait));
            IsValid = WeaponName != null;
        }
    }
}
