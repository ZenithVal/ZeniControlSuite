using Microsoft.AspNetCore.Components;
using MudBlazor;
using Newtonsoft.Json;
using ZeniControlSuite.Models.BindingTrees;
using ZeniControlSuite.Components.Pages;

namespace ZeniControlSuite.Services;


public class Service_BindingTrees : IHostedService
{
    [Inject] private Service_Points PointsService { get; set; } = default!;
    [Inject] private Service_Logs LogService { get; set; } = default!;
    private void Log(string message, Severity severity)
    {
        LogService.AddLog("Service_BindingTrees", "System", message, severity);
    }

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
            CheckBindingRelations();
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
    public Color lastLogColor = Color.Default;
    public Severity lastLogSeverity = Severity.Normal;

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
            Log($"Error loading Binding Trees:\n{e.Message}", Severity.Error);
            Bindings.pageEnabled = false;
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
                    Console.WriteLine($"Validating Prereqs of {binding.Name}");
                    foreach (string prerequisite in binding.Prerequisites)
                    {
                        if (bindingTrees.SelectMany(tree => tree.Bindings).FirstOrDefault(b => b.Name == prerequisite) == null)
                        {
                            Log($"Binding Tree Error: {binding.Name} has a prerequisite of {prerequisite} which doesn't exist.", Severity.Error);
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
                            Log($"Binding Tree Error: {binding.Name} has a conflict of {conflict} which doesn't exist.", Severity.Error);
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
                            Log($"Binding Tree Error: {binding.Name} has a replace of {replace} which doesn't exist.", Severity.Error);
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
    public void CheckBindingRelations()
    {
        foreach (Binding binding in bindingsList)
        {
            if (!binding.Conflicts.Any(conflict => GetBindingByName(conflict).isOwned))
            {
                binding.isConflictOwned = false;
            }
            else
            {
                binding.isConflictOwned = true;
            }

            if (binding.Prerequisites.Count == 0)
            {
                binding.isPrereqMet = true;
                binding.isSubPrereqMet = true;
            }
            else
            {
                if (binding.Prerequisites.All(prereq => GetBindingByName(prereq).isOwned))
                {
                    binding.isPrereqMet = true;
                    binding.isSubPrereqMet = true;
                }
                else
                {
                    binding.isPrereqMet = false;
                    if (binding.Prerequisites.Any(prereq => !GetBindingByName(prereq).isPrereqMet))
                    {
                        binding.isSubPrereqMet = false;
                    }
                    else
                    {
                        binding.isSubPrereqMet = true;
                    }
                }
            }

            if (binding.Replaces.Count > 0)
            {
                if (binding.Replaces.Any(replace => GetBindingByName(replace).isLocked) || binding.Replaces.Any(replace => GetBindingByName(replace).isReplaceLocked))
                {
                    binding.isReplaceLocked = true;
                }
                else
                {
                    binding.isReplaceLocked = false;
                }
            }
        }
        InvokeBindingTreeUpdate();
    }

    public void InvokeBindingTreeUpdate()
    {
        if (OnBindingTreeUpdate != null)
        {
            OnBindingTreeUpdate.Invoke();
        }
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
