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

        public ClassifiedScreen(Codex codex, IEnumerable<Slot> inSlots)
        {
            Slots = inSlots.ToList();
            WeaponName = Codex.DetermineWeapon(Slots.Select(s => s.Trait));

            IsValid = WeaponName != null;
        }
    }
}
