namespace HadesBoonBot.Classifiers
{
    internal class ClassifiedScreen
    {
        public string? WeaponName;
        public List<Slot> Slots;
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
                return $"{Col}_{Row}: {Trait}";
            }
        }

        /// <summary>
        /// Construct a classification result from a list of trait slots, do some cleanup etc
        /// </summary>
        /// <param name="codex">Trait list</param>
        /// <param name="inSlots">Classified traits</param>
        public ClassifiedScreen(Codex codex, IEnumerable<Slot> inSlots)
        {
            //detect when we run out of traits and trim them off
            var backEmpties = inSlots
                .Reverse()
                .TakeWhile(s => !Codex.IsSlotFilled(s.Trait))
                .Count();

            Slots = inSlots.Take(inSlots.Count() - backEmpties).ToList();

            WeaponName = Codex.DetermineWeapon(Slots.Select(s => s.Trait));
            IsValid = WeaponName != null;
        }
    }
}
