namespace HadesBoonBot.Classifiers
{
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
        /// Construct a classification result from a list of trait slots, do some cleanup etc
        /// </summary>
        /// <param name="codex">Trait list</param>
        /// <param name="inSlots">Classified traits</param>
        public ClassifiedScreen(Codex codex, IEnumerable<Slot> inSlots)
        {
            //split into tray & pins
            Slots = inSlots.Where(s => s.Col != -1).ToList();
            PinSlots = inSlots.Where(s => s.Col == -1).ToList();

            //detect when we run out of traits and trim them off
            var backEmpties = Slots
                .AsEnumerable()
                .Reverse()
                .TakeWhile(s => !Codex.IsSlotFilled(s.Trait))
                .Count();

            Slots = inSlots.Take(inSlots.Count() - backEmpties).ToList();

            WeaponName = Codex.DetermineWeapon(Slots.Select(s => s.Trait));
            IsValid = WeaponName != null;
        }
    }
}
