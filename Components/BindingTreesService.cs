using Microsoft.AspNetCore.Components;
using MudBlazor;
using Newtonsoft.Json;
namespace ZeniControlSuite.Components;

public class BindingTreesService : IHostedService
{
    [Inject]
    private GamesPointsService Points { get; set; } = default!;


    //===========================================//
    #region HostedService Stuff
    public delegate void BindingTreeUpdate();
    public event BindingTreeUpdate? OnBindingTreeUpdate;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        InitializeBindingTrees();

        if (bindingTrees.Count == 0)
        {
            Console.WriteLine("No Binding Trees Found");
        }
        else
        {
            ValidateBindingTreesJson();
            Console.WriteLine("BindingTreesService Started");
        }
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    #endregion

    public List<BindingTree> bindingTrees = new();
    public List<Binding> bindingsList = new();
    public Padlocks padlocks = new();
    public string lastLog = "";

    //===========================================//
    #region Initialization & Binding Tree Managmenet
    private void InitializeBindingTrees()
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
            Console.WriteLine($"Error loading Binding Trees:\n{e.Message}");
        }
    }

    private void ValidateBindingTreesJson()
    {
        //Check all the binding jsons for errors that'll crash the app
        foreach (BindingTree tree in bindingTrees)
        {
            Console.WriteLine($"Validating {tree.Name}");
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
    #endregion


    //===========================================//
    #region Binding Functions
    //==============================
    // Binding Actions
    public void ShowBindingInfo(Binding binding)
    {
        //formMain.writeConsoleUI($"{binding.Name} ~ {binding.Description}", formMain.CC.Info);
    }

    //snackbar
    public void Notify(string message, Color color)
    {
        
    }

    public void InvokeBindingTreeUpdate()
    {
        if (OnBindingTreeUpdate != null)
        {
            OnBindingTreeUpdate.Invoke();
        }
    }

    public void addBinding(Binding binding)
    {

    }

    public void removeBinding(Binding binding)
    {

    }

    #endregion


    //===========================================//
    #region Info Functions
    //==============================
    // Info Functions

    public Padlocks GetPadlocks()
    {
        return padlocks;
    }

    public Binding GetBindingByName(string bindingName)
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


    //===========================================//
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
