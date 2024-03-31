using Microsoft.AspNetCore.Components;
using Newtonsoft.Json;
using static ZeniControlSuite.Components.Pages.BindingManager;

namespace ZeniControlSuite.Components.Pages;
public partial class BindingManager : ComponentBase 
{
    public static bool pageEnabled = true;

    [Inject]
    private GamesPointsService Points { get; set; } = default!;

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

        public bool isPrereqMet = false; //Is the item's prereq met?
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
        public int Owned = 0;
        public int Used = 0;
    }

    private List<BindingTree> bindingTrees = new List<BindingTree>();
    private List<Binding> bindingsList = new List<Binding>();
    private Padlocks padlocks;

    // Old code for winforms, replace.
    //private static Dictionary<string, Button> buttonBindings = new Dictionary<string, Button>();
    //private static Dictionary<string, System.Windows.Forms.Timer> temporaryTimers = new Dictionary<string, System.Windows.Forms.Timer>();

    #region Initialization & Binding Tree Managmenet
    protected override async Task OnInitializedAsync()
    {
        if (bindingTrees.Count == 0)
        {
            InitializeBindingTrees();
            ValidateBindingTreesJson();
        }
    }

    public void InitializeBindingTrees()
    {
        try
        {
            string json = File.ReadAllText("Configs/BindingTrees.json");
            bindingTrees = JsonConvert.DeserializeObject<List<BindingTree>>(json);

            bindingsList = bindingTrees.SelectMany(tree => tree.Bindings).ToList();
            padlocks = bindingTrees.FirstOrDefault().Padlocks;
        }
        catch (Exception e)
        {
            //formMain.writeConsoleUI($"Error loading Binding Trees, see console for error.", formMain.CC.Failure);
            Console.WriteLine($"Error loading Binding Trees:\n{e.Message}");
        }
    }

    public void ValidateBindingTreesJson()
    {
        //Check all the binding jsons for errors that'll crash the app
        foreach (BindingTree tree in bindingTrees)
        {
            foreach (Binding binding in tree.Bindings)
            {
                Console.WriteLine($"Validating {binding.Name}");
                if (binding.Prerequisites.Count > 0)
                {
                    foreach (string prerequisite in binding.Prerequisites)
                    {
                        if (bindingTrees.SelectMany(tree => tree.Bindings).FirstOrDefault(b => b.Name == prerequisite) == null)
                        {
                            Console.WriteLine($"Binding Tree Error: {binding.Name} has a prerequisite of {prerequisite} which doesn't exist.");
                            binding.Prerequisites.Remove(prerequisite);
                        }
                    }
                }

                if (binding.Conflicts.Count > 0)
                {
                    foreach (string conflict in binding.Conflicts)
                    {
                        if (bindingTrees.SelectMany(tree => tree.Bindings).FirstOrDefault(b => b.Name == conflict) == null)
                        {
                            Console.WriteLine($"Binding Tree Error: {binding.Name} has a conflict of {conflict} which doesn't exist.");
                            //Remove the conflict to prevent errors
                            binding.Conflicts.Remove(conflict);
                        }
                    }
                }

                if (binding.Replaces.Count > 0)
                {
                    foreach (string replace in binding.Replaces)
                    {
                        if (bindingTrees.SelectMany(tree => tree.Bindings).FirstOrDefault(b => b.Name == replace) == null)
                        {
                            Console.WriteLine($"Binding Tree Error: {binding.Name} has a replace of {replace} which doesn't exist.");
                            //Remove the replace to prevent errors
                        }
                    }
                }
            }
        }
    }

    public void BindingTreeGenerate()
    {
        //TODO
    }

    public void BindingTreeUpdate()
    {
        //TODO
    }
    #endregion


    #region Info Functions
    //==============================
    // Info Functions

    public List<BindingTree> GetBindingTrees()
    {
        return bindingTrees;
    }

/*    private List<Binding> GetBindingsByTree(string treeName)
    {
        //return bindingTrees.FirstOrDefault(tree => tree.Name == treeName)?.Bindings ?? new List<Binding>();
    }*/

    private Binding GetBindingByName(string bindingName)
    {
        return bindingTrees.SelectMany(tree => tree.Bindings).FirstOrDefault(binding => binding.Name == bindingName);
    }

    public string GetOwnedBindings()
    {
        string ownedBindings = "";

        foreach (Binding binding in bindingTrees.SelectMany(tree => tree.Bindings))
        {
            if (binding.isOwned)
            {
                ownedBindings += $"{binding.Name}";
                if (binding.isLocked)
                {
                    ownedBindings += " 🔒";
                }
                ownedBindings += "\n";
            }
        }

        return ownedBindings;
    }

    #endregion

    #region Binding Functions
    //==============================
    // Binding Actions
    public void ShowBindingInfo(Binding binding)
    {
        //formMain.writeConsoleUI($"{binding.Name} ~ {binding.Description}", formMain.CC.Info);
    }

    public void BuyBinding(Binding binding)
    {
        if (binding.isBuyable)
        {
            //Confirm if it's buyable
            if (binding.Prerequisites.Count > 0)
            {
                string preReqsNeeded = "";

                foreach (string prerequisite in binding.Prerequisites)
                {
                    Binding prerequisiteBinding = GetBindingByName(prerequisite);

                    if (!prerequisiteBinding.isOwned)
                    {
                        preReqsNeeded += $"({prerequisite}), ";
                    }
                }

                if (preReqsNeeded != "")
                {
                    preReqsNeeded = StringFormatCommaList(preReqsNeeded);
                    //formMain.writeConsoleUI($"Can't Add ({binding.Name}) ~ {preReqsNeeded} needed.", formMain.CC.Warning);
                    return;
                }
            }

            if (binding.Conflicts.Count > 0)
            {
                string conflicts = "";

                foreach (string conflict in binding.Conflicts)
                {
                    Binding conflictBinding = GetBindingByName(conflict);

                    if (conflictBinding.isOwned)
                    {
                        conflicts += $"({conflict}), ";
                    }
                }

                if (conflicts != "")
                {
                    conflicts = StringFormatCommaList(conflicts);
                    //formMain.writeConsoleUI($"Can't Add ({binding.Name}) ~ {conflicts} already owned.", formMain.CC.Warning);
                    return;
                }
            }

            if (binding.Replaces.Count > 0)
            {
                string replacesLocked = "";

                foreach (string replace in binding.Replaces)
                {
                    Binding replaceBinding = GetBindingByName(replace);

                    if (replaceBinding.isLocked)
                    {
                        replacesLocked += $"({replace}), ";
                    }
                }

                if (replacesLocked != "")
                {
                    replacesLocked = StringFormatCommaList(replacesLocked);
                    //formMain.writeConsoleUI($"Can't Add ({binding.Name}) ~ {replacesLocked} locked.", formMain.CC.Warning);
                    return;
                }
            }

            if (Points.pointsTotal < binding.PointValue)
            {
                //formMain.writeConsoleUI($"Can't add ({binding.Name}) ~ Need {binding.PointValue}p", formMain.CC.Warning);
                return;
            }

            if (binding.GameEnder)
            {
                //formMain.writeConsoleUI($"({binding.Name}) is probably a final purchase. Click again to confirm.", formMain.CC.Warning);
                binding.GameEnder = false;
                return;
            }

            //Successfully Bought
            binding.isOwned = true;
            binding.isBuyable = false;
            Points.UpdatePoints(-binding.PointValue);
            //formMain.writeConsoleUI($"Added ({binding.Name}) for {binding.PointValue}p", formMain.CC.Success);

            //Check if it's a consumable item and there's one available
            if (binding.ConsumableCount != -1)
            {
                if (binding.ConsumableCount > 0)
                {
                    binding.ConsumableCount--;

                    if (binding.ConsumableCount >= 1)
                    {
                        binding.isOwned = false;
                        binding.isBuyable = true;
                    }
                    //formMain.bindingTreeUIUpdatesNeeded = true;
                    return;
                }
                else
                {
                    binding.ConsumableCount = 0;
                    //formMain.writeConsoleUI($"Can't add ({binding.Name}) ~ No more available", formMain.CC.Warning);
                    //formMain.bindingTreeUIUpdatesNeeded = true;
                    return;
                }
            }

            if (binding.CanBeSold)
            {
                binding.isSellable = true;
            }

            //If its got Prereqs, set isPrereqLocked to true for those items
            if (binding.Prerequisites.Count > 0)
            {
                foreach (string prerequisite in binding.Prerequisites)
                {
                    Binding prerequisiteBinding = GetBindingByName(prerequisite);
                    prerequisiteBinding.isPrereqLocked = true;
                }
            }

            if (binding.Replaces.Count > 0)
            {
                foreach (string replace in binding.Replaces)
                {
                    Binding replaceBinding = GetBindingByName(replace);
                    replaceBinding.isReplaceLocked = true;
                }
            }

            /* // A temporary item sounds like hell in Blazor
            //If it's a temporary item, start a timer for it.
            if (binding.TempDuration > 0)
            {
                //formMain.addToConsoleUI($" for {binding.TempDuration} Minutes");
                System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                timer.Interval = (int)(binding.TempDuration * 60 * 1000);
                timer.Tick += (sender, e) =>
                {
                    //formMain.writeConsoleUI($"({binding.Name}) expired.", formMain.CC.Info);
                    timer.Stop();
                    timer.Dispose();
                    formMain.playSound("Ping");
                    binding.isOwned = false;
                    if (binding.CanBeSold)
                    {
                        binding.isSellable = false;
                    }
                    binding.isBuyable = true;
                    formMain.bindingTreeUIUpdatesNeeded = true;
                };
                timer.Start();
                //temporaryTimers[binding.Name] = timer;
            }
            */

            //formMain.bindingTreeUIUpdatesNeeded = true;
            return;
        }
        else
        {
            //Not Buyable error
            //formMain.writeConsoleUI($"Cannot buy ({binding.Name}). The button should've been disabled?", color: formMain.CC.Failure);
            //formMain.bindingTreeUIUpdatesNeeded = true;
            return;
        }
    }

/*    public static void BuyRandomBinding(bool noCost = false)
    {
        List<Binding> availableBindings = new List<Binding>();

        foreach (Binding binding in bindingTrees.SelectMany(tree => tree.Bindings))
        {
            if (binding.isBuyable &&
            binding.isPrereqMet &&
            binding.ConsumableCount == -1 &&
            binding.TempDuration == 0 &&
            binding.Conflicts.Count == 0 &&
            binding.CanBeSold &&
            !binding.Name.Contains("Toy"))
            {
                if (Points.pointsTotal >= binding.PointValue)
                {
                    availableBindings.Add(binding);
                }
                else if (noCost)
                {
                    availableBindings.Add(binding);
                }
            }
        }

        //buy one of them randomly
        if (availableBindings.Count > 0)
        {
            Binding bindingToBuy = availableBindings[RandomInt(0, availableBindings.Count)];
            if (noCost)
            {
                Points.UpdatePoints(bindingToBuy.PointValue);
            }
            BuyBinding(bindingToBuy);
            if (formMain.consoleUIColor == formMain.CC.Success)
            {
                formMain.addToConsoleUI(" Randomly");
            }

        }
        else
        {
            formMain.writeConsoleUI($"No available bindings to buy.", formMain.CC.Failure);
        }
    }
*/

    public void SellBinding(Binding binding)
    {
        if (binding.CanBeSold)
        {
            //Make sure it's sellable
            if (binding.isLocked)
            {
                //formMain.writeConsoleUI($"Cannot Remove {binding.Name}. It's locked", color: formMain.CC.Warning);
                //formMain.bindingTreeUIUpdatesNeeded = true;
                return;
            }

            if (binding.isPrereqLocked)
            {
                string prereqOfOwned = "";
                //Look through all the bindings for anything that has this binding as a prereq
                foreach (Binding otherBinding in bindingTrees.SelectMany(tree => tree.Bindings))
                {
                    if (otherBinding.Prerequisites.Contains(binding.Name) && otherBinding.isOwned)
                    {
                        prereqOfOwned += $"({otherBinding.Name}), ";
                    }
                }

                prereqOfOwned = StringFormatCommaList(prereqOfOwned);
                //formMain.bindingTreeUIUpdatesNeeded = true;
                //formMain.writeConsoleUI($"Cannot Remove {binding.Name}. It's a prerequisite of ({prereqOfOwned}).", formMain.CC.Warning);
                return;
            }

            //Successfully Sold
            binding.isOwned = false;
            binding.isSellable = false;
            binding.isBuyable = true;
            Points.UpdatePoints(binding.PointValue);
            //formMain.writeConsoleUI($"Removed {binding.Name} ~ Refunded {binding.PointValue}p", formMain.CC.Success);

            //if it's got prereqs and this is the only item that has it as a prereq, set isPrereqLocked to false for those items
            if (binding.Prerequisites.Count > 0)
            {
                bool isOnlyPrereq = true;
                foreach (Binding otherBinding in bindingTrees.SelectMany(tree => tree.Bindings))
                {
                    if (otherBinding.Prerequisites.Contains(binding.Name) && otherBinding.isOwned)
                    {
                        isOnlyPrereq = false;
                    }
                }

                if (isOnlyPrereq)
                {
                    foreach (string prerequisite in binding.Prerequisites)
                    {
                        Binding prerequisiteBinding = GetBindingByName(prerequisite);
                        prerequisiteBinding.isPrereqLocked = false;
                    }
                }
            }

            //If it's got replaces, set isReplaceLocked to false for those items
            if (binding.Replaces.Count > 0)
            {
                foreach (string replace in binding.Replaces)
                {
                    Binding replaceBinding = GetBindingByName(replace);
                    replaceBinding.isReplaceLocked = false;
                }
            }

            //formMain.bindingTreeUIUpdatesNeeded = true;
            return;
        }
        else
        {
            //formMain.writeConsoleUI($"Cannot Remove {binding}. The button should've been disabled?", formMain.CC.Failure);
            //formMain.bindingTreeUIUpdatesNeeded = true;
            return;
        }
    }

    /* Locks
    public void LockBinding(Binding binding)
    {
        if (binding.CanBeLocked)
        {
            if (!binding.isOwned)
            {
                //formMain.writeConsoleUI($"Cannot Lock {binding.Name}. It's not owned.", formMain.CC.Failure);
                //formMain.bindingTreeUIUpdatesNeeded = true;
                return;
            }
            if (binding.isLocked)
            {
                //formMain.writeConsoleUI($"{binding.Name}. Is already locked.", formMain.CC.Failure);
                //formMain.bindingTreeUIUpdatesNeeded = true;
                return;
            }
            //If it's a temporary item, stop the timer
            if (binding.TempDuration > 0)
            {
                //System.Windows.Forms.Timer timer = temporaryTimers[binding.Name];
                //timer.Stop();
                //timer.Dispose();
            }
            //Lock the item
            binding.isLocked = true;
            binding.isSellable = false;
            padlocks.Owned--;
            padlocks.Used++;
            //formMain.writeConsoleUI($"Locked {binding.Name}.", formMain.CC.Success);
            //formMain.bindingTreeUIUpdatesNeeded = true;
            return;
        }
        else
        {
            //formMain.writeConsoleUI($"Cannot Lock {binding}. Why did it have a lock option?", formMain.CC.Failure);
            return;
        }
    }

    public void BuyLock()
    {
        //If total locks less than limit, has enough points, and not already owned, buy it.
        if ((padlocks.Owned + padlocks.Used) < padlocks.Limit)
        {
            if (padlocks.Cost <= Points.pointsTotal)
            {
                Points.UpdatePoints(-padlocks.Cost);
                padlocks.Owned++;
                //formMain.writeConsoleUI($"Bought Lock ({padlocks.Owned + padlocks.Used} of {padlocks.Limit}) for {padlocks.Cost}", formMain.CC.Success);
                padlocks.Cost += padlocks.CostIncrease;
            }
            else
            {
                //formMain.writeConsoleUI($"Cannot buy lock. Not enough points.", formMain.CC.Failure);
            }
        }
        else
        {
            //formMain.writeConsoleUI($"Cannot buy lock. Limit reached.", formMain.CC.Failure);
        }

        //formMain.bindingTreeUIUpdatesNeeded = true;
    }*/
    #endregion


    #region Misc Functions

    public string StringFormatCommaList(string input)
    {
        //Cleanup the string, remove the last comma and space
        input = input.Remove(input.Length - 2);

        if (input.Contains(","))
        {
            if (input.IndexOf(",") == input.LastIndexOf(",")) //If there's only one comma
            {
                input = input.Remove(input.IndexOf(","), 1).Insert(input.IndexOf(","), " and");
            }
            else //If there's more than one comma, replace the last one with "and"
            {
                input = input.Remove(input.LastIndexOf(","), 1).Insert(input.LastIndexOf(","), ", and");
            }
        }

        //We love grammar. Add "is" if it's a single item, "are" if it's a list.
        string isAnd = "is";
        if (input.Contains(","))
        {
            isAnd = "are";
        }

        input = $"{input} {isAnd}";
        return input;
    }

    public int RandomInt(int min, int max)
    {
        Random random = new Random();
        return random.Next(min, max);
    }

    #endregion
}