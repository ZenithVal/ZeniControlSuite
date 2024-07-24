using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using ZeniControlSuite.Services;
using ZeniControlSuite.Models.BindingTrees;

namespace ZeniControlSuite.Components.Pages;

public partial class Bindings : IDisposable
{
    public static bool pageEnabled = true;

    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;
    [Inject] private Service_Logs LogService { get; set; } = default!;
    [Inject] private Service_Points PointsService { get; set; } = default!;
    [Inject] private Service_BindingTrees BindingTreesService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private static Dictionary<string, System.Timers.Timer> temporaryTimers = new Dictionary<string, System.Timers.Timer>();
    private static string hoverBindingDescription = "";

    private string user = "Undefined"; //Will later be replaced with the user's name via discord Auth
    private AuthenticationState context;
    private readonly string pageName = "Binding Manager";

    protected override async Task OnInitializedAsync()
    {
        PointsService.OnPointsUpdate += OnPointsUpdate;
        BindingTreesService.OnBindingTreeUpdate += OnBindingTreeUpdate;

        var context = await AuthProvider.GetAuthenticationStateAsync();
        user = context.GetUserName();
        LogService.AddLog(pageName, user, "PageLoad", Severity.Normal);

    }

    protected override void OnParametersSet()
    {
    }

    private void OnPointsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnBindingTreeUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    private void btnHoverShowInfo(Binding binding)
    {
        hoverBindingDescription = binding.Description;
    }

    private void Log(string message, Severity severity)
    {
        LogService.AddLog(pageName, user, message, severity);

        //Console.WriteLine(DateTime.Now + " | " + message);
        Snackbar.Add(message, severity);
        BindingTreesService.lastLog = message;
        BindingTreesService.lastLogSeverity = severity;
        BindingTreesService.lastLogColor = severity switch {
            Severity.Error => Color.Error,
            Severity.Info => Color.Info,
            Severity.Success => Color.Success,
            Severity.Warning => Color.Warning,
            _ => Color.Default,
        };
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
                    Binding prerequisiteBinding = BindingTreesService.GetBindingByName(prerequisite);

                    if (!prerequisiteBinding.isOwned)
                    {
                        preReqsNeeded += $"({prerequisite}), ";
                    }
                }

                if (preReqsNeeded != "")
                {
                    preReqsNeeded = BindingTreesService.StringFormatCommaList(preReqsNeeded);
                    Log($"Can't add ({binding.Name}) ~ {preReqsNeeded} needed.", Severity.Warning);
                    return;
                }
            }

            if (binding.Conflicts.Count > 0)
            {
                string conflicts = "";

                foreach (string conflict in binding.Conflicts)
                {
                    Binding conflictBinding = BindingTreesService.GetBindingByName(conflict);

                    if (conflictBinding.isOwned)
                    {
                        conflicts += $"({conflict}), ";
                    }
                }

                if (conflicts != "")
                {
                    conflicts = BindingTreesService.StringFormatCommaList(conflicts);
                    Log($"Can't add ({binding.Name}) ~ {conflicts} is already owned.", Severity.Warning);
                    return;
                }
            }

            if (binding.Replaces.Count > 0)
            {
                string replacesLocked = "";

                foreach (string replace in binding.Replaces)
                {
                    Binding replaceBinding = BindingTreesService.GetBindingByName(replace);

                    if (replaceBinding.isLocked)
                    {
                        replacesLocked += $"({replace}), ";
                    }
                }

                if (replacesLocked != "")
                {
                    replacesLocked = BindingTreesService.StringFormatCommaList(replacesLocked);
                    Log($"Can't add ({binding.Name}) ~ pre-requisite {replacesLocked} is locked.", Severity.Warning);
                    return;
                }
            }

            if (PointsService.pointsTotal < binding.PointValue)
            {
                Log($"Can't add ({binding.Name}) ~ Need {binding.PointValue}p", Severity.Warning);
                return;
            }

            if (binding.GameEnder)
            {
                Log($"({binding.Name}) is probably a final purchase. Click again to confirm.", Severity.Warning);
                binding.GameEnder = false;
                return;
            }

            //Successfully Bought
            binding.isOwned = true;
            binding.isBuyable = false;
            PointsService.UpdatePoints(-binding.PointValue);
            Log($"Added ({binding.Name}) for {binding.PointValue}p", Severity.Success);
            BindingTreesService.CheckBindingRelations();

            //Check if it's a consumable item and there's one available
            if (binding.ConsumableCount != -1)
            {
                binding.isOwned = false;
                if (binding.ConsumableCount > 0)
                {
                    binding.ConsumableCount--;

                    if (binding.ConsumableCount >= 1)
                    {
                        binding.isOwned = false;
                        binding.isBuyable = true;
                    }
                    BindingTreesService.InvokeBindingTreeUpdate();
                    return;
                }
                else
                {
                    binding.ConsumableCount = 0;
                    Log($"Can't add ({binding.Name}) ~ No more available", Severity.Warning);
                    BindingTreesService.InvokeBindingTreeUpdate();
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
                    Binding prerequisiteBinding = BindingTreesService.GetBindingByName(prerequisite);
                    prerequisiteBinding.isParentOwned = true;
                }
            }

            if (binding.Replaces.Count > 0)
            {
                foreach (string replace in binding.Replaces)
                {
                    Binding replaceBinding = BindingTreesService.GetBindingByName(replace);
                    replaceBinding.isReplacerOwned = true;
                }
            }

            //If it's a temporary item, start a timer for it.
            if (binding.TempDuration > 0)
            {
                Log($"({binding.Name}) activated for {binding.TempDuration} Minutes", Severity.Info);

                System.Timers.Timer timer = new System.Timers.Timer();

                timer.Interval = (int)(binding.TempDuration * 60 * 1000);
                timer.Elapsed += (sender, e) =>
                {
                    Log($"({binding.Name}) expired.", Severity.Info);
                    timer.Stop();
                    timer.Dispose();
                    //formMain.playSound("Ping");
                    binding.isOwned = false;
                    if (binding.CanBeSold)
                    {
                        binding.isSellable = false;
                    }
                    binding.isBuyable = true;
                    BindingTreesService.InvokeBindingTreeUpdate();
                };
                timer.Start();
                temporaryTimers[binding.Name] = timer;
            }

            BindingTreesService.InvokeBindingTreeUpdate();
            return;
        }
        else
        {
            Log($"Cannot buy ({binding.Name}).", Severity.Error);
            BindingTreesService.InvokeBindingTreeUpdate();
            return;
        }
    }

    public void SellBinding(Binding binding)
    {
        if (binding.CanBeSold)
        {
            //Make sure it's sellable
            if (binding.isLocked)
            {
                Log($"Cannot Remove {binding.Name}. It's locked", Severity.Warning);
                BindingTreesService.InvokeBindingTreeUpdate();
                return;
            }

            if (binding.isParentOwned)
            {
                string prereqOfOwned = "";
                //Look through all the bindings for anything that has this binding as a prereq
                foreach (Binding otherBinding in BindingTreesService.bindingTrees.SelectMany(tree => tree.Bindings))
                {
                    if (otherBinding.Prerequisites.Contains(binding.Name) && otherBinding.isOwned)
                    {
                        prereqOfOwned += $"({otherBinding.Name}), ";
                    }
                }

                prereqOfOwned = BindingTreesService.StringFormatCommaList(prereqOfOwned);
                BindingTreesService.InvokeBindingTreeUpdate();
                Log($"Cannot Remove {binding.Name}. It's a prerequisite of ({prereqOfOwned}).", Severity.Warning);
                return;
            }

            //Successfully Sold
            binding.isOwned = false;
            binding.isSellable = false;
            binding.isBuyable = true;
            PointsService.UpdatePoints(binding.PointValue);
            Log($"Removed {binding.Name} ~ Refunded {binding.PointValue}p", Severity.Normal);
            BindingTreesService.CheckBindingRelations();

            //if it's got prereqs and this is the only item that has it as a prereq, set isPrereqLocked to false for those items
            if (binding.Prerequisites.Count > 0)
            {
                bool isOnlyPrereq = true;
                foreach (Binding otherBinding in BindingTreesService.bindingTrees.SelectMany(tree => tree.Bindings))
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
                        Binding prerequisiteBinding = BindingTreesService.GetBindingByName(prerequisite);
                        prerequisiteBinding.isParentOwned = false;
                    }
                }
            }

            //If it's got replaces, set isReplaceLocked to false for those items
            if (binding.Replaces.Count > 0)
            {
                foreach (string replace in binding.Replaces)
                {
                    Binding replaceBinding = BindingTreesService.GetBindingByName(replace);
                    replaceBinding.isReplacerOwned = false;
                }
            }

            BindingTreesService.InvokeBindingTreeUpdate();
            return;
        }
        else
        {
            Log($"Cannot Remove {binding}. The button should've been disabled?", Severity.Error);
            BindingTreesService.InvokeBindingTreeUpdate();
            return;
        }
    }

    public void BuyRandomBinding()
    {
        List<Binding> buyableBindings = new List<Binding>();

        foreach (Binding binding in BindingTreesService.bindingTrees.SelectMany(tree => tree.Bindings))
        {
            if (binding.isBuyable && binding.ConsumableCount == 0 && binding.GameEnder == false)
            {
                buyableBindings.Add(binding);
            }
        }

        if (buyableBindings.Count > 0)
        {
            Random random = new Random();
            int randomIndex = random.Next(0, buyableBindings.Count);
            BuyBinding(buyableBindings[randomIndex]);
        }
        else
        {
            Log("No buyable items found.", Severity.Warning);
        }
    }

    public void LockBinding(Binding binding)
    {
        if (binding.CanBeLocked)
        {
            if (!binding.isOwned)
            {
                Log($"Cannot Lock {binding.Name}. It's not owned.", Severity.Error);
                BindingTreesService.InvokeBindingTreeUpdate();
                return;
            }
            if (binding.isLocked)
            {
                Log($"{binding.Name}. Is already locked.", Severity.Error);
                BindingTreesService.InvokeBindingTreeUpdate();
                return;
            }
            //If it's a temporary item, stop the timer
            if (binding.TempDuration > 0)
            {
                System.Timers.Timer timer = temporaryTimers[binding.Name];
                timer.Stop();
                timer.Dispose();
            }
            //Lock the item
            binding.isLocked = true;
            binding.isSellable = false;
            BindingTreesService.padlocks.Owned--;
            BindingTreesService.padlocks.Used++;
            Log($"Locked {binding.Name}.", Severity.Success);
            BindingTreesService.CheckBindingRelations();
            BindingTreesService.InvokeBindingTreeUpdate();
            return;
        }
        else
        {
            Log($"Cannot Lock {binding}. Why did it have a lock option?", Severity.Error);
            return;
        }
    }

    public void BuyPadlock()
    {
        //If total locks less than limit, has enough points, and not already owned, buy it.
        if ((BindingTreesService.padlocks.OwnedUsed) < BindingTreesService.padlocks.Limit)
        {
            if (BindingTreesService.padlocks.Cost <= PointsService.pointsTotal)
            {
                PointsService.UpdatePoints(-BindingTreesService.padlocks.Cost);
                BindingTreesService.padlocks.Owned++;
                Log($"Bought Lock ({BindingTreesService.padlocks.Owned + BindingTreesService.padlocks.Used} of {BindingTreesService.padlocks.Limit}) for {BindingTreesService.padlocks.Cost}", Severity.Success);
                BindingTreesService.padlocks.Cost += BindingTreesService.padlocks.CostIncrease;
            }
            else
            {
                Log($"Cannot buy lock. Not enough points.", Severity.Warning);
            }
        }
        else
        {
            Log($"Cannot buy lock. Limit reached.", Severity.Error);
        }

        BindingTreesService.InvokeBindingTreeUpdate();
    }

    public void Dispose()
    {
        PointsService.OnPointsUpdate -= OnPointsUpdate;
        BindingTreesService.OnBindingTreeUpdate -= OnBindingTreeUpdate;
    }





}