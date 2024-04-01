namespace ZeniControlSuite.Components
{
    public class BindingTree
    {
        public string Name { get; set; }
        public List<Binding> Bindings { get; set; }
        public Padlocks Padlocks { get; set; }
    }

    public class Position
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class Binding
    {
        public string Name { get; set; } //Name of the item.
        public Position Position { get; set; } //Position on the tree
        public string Description { get; set; } //Description of the item.
        public double PointValue { get; set; } //How much the item costs.

        public List<string> Prerequisites { get; set; } //List of items that need to be bought before this item can be bought.
        public List<string> Conflicts { get; set; } //List of items that can't be bought if this item is bought.
        public List<string> Replaces { get; set; } //A restraint that replaces the prereq item as an "upgrade".
                                                   //(EG, A alternate version of an item) Can't be bought if item that it replaces is locked sicne they can't take off the previous item.

        public double TempDuration { get; set; } //If greater than 0, Item lasts for this duration in minutes before being removed.
                                                 //If something is bought that has it as a prereq, its timer pauses.
                                                 //If the item that made it pause is sold, the timer continues

        public int ConsumableCount { get; set; } //If greater than -1, It's a limited use consumable item that can be bought this many times.
                                                 //Can't be sold. (EG: Buy a new rule that opponent needs to follow)

        //Information Variables
        public bool CanBeLocked { get; set; } //Can the item be locked?
        public bool CanBeSold { get; set; } //Can the item be sold once bought?
        public bool GameEnder { get; set; } //If true, warns user that buying this item will probably be the last buyable item.


        //Current Binding Variables. Updated by the app based on events and factors.
        //Used for determining what should be displayed on the item label and button.
        public bool isBuyable { get; set; } //Can the item be bought?
        public bool isSellable { get; set; } //Can the item be sold?
        public bool isPrereqMet { get; set; } = false; //Is the item's prereq met?
        public bool isSubPrereqMet { get; set; } = true; //Are the prereq's prereqs met?
        public bool isPrereqLocked { get; set; } //Is it disabled because it's a prereq of a owned item?
        public bool isConflictLocked { get; set; } //Is it disabled because it's a conflict of a owned item?
        public bool isReplaceLocked { get; set; } //Mainly used for blocking locking an item if its been replaced.
        public bool isOwned { get; set; } //Is the item owned?
        public bool isLocked { get; set; } //Is the item locked?

    }

    public class Padlocks
    {
        public bool Enabled { get; set; }
        public Position Position { get; set; }
        public double Cost { get; set; }
        public double CostIncrease { get; set; }
        public int Limit { get; set; }
        public int Owned { get; set; } = 0;
        public int Used { get; set; } = 0;
        public int OwnedUsed => Owned + Used;
    }
}
