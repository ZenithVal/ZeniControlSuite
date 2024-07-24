using Microsoft.AspNetCore.Components;
using MudBlazor;
using ZeniControlSuite.Models.Games;

namespace ZeniControlSuite.Services;

public class Service_Games : IHostedService
{
    private readonly Service_Logs LogService;
    public Service_Games(Service_Logs serviceLogs) { LogService = serviceLogs; }
    private void Log(string message, Severity severity)
    {
        LogService.AddLog("Service_Games", "System", message, severity, Variant.Outlined);
    }

    //===========================================//
    #region HostedService Stuff
    public delegate void RequestGamesUpdate();
    public event RequestGamesUpdate? OnGamesUpdate;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            ParseGames();
            Log("Service Started", Severity.Normal);
            Console.WriteLine("");
        }
        catch (Exception e)
        {
            gamesList.Add(new Game { Name = "Error", Description = new List<Game.DescriptionLine> { new Game.DescriptionLine { typo = Typo.body1, text = "Games.ini parsing failed: " + e.Message } } });
            Log("Games.ini parsing failed: " + e.Message, Severity.Error);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void Update()
    {
        if (OnGamesUpdate != null)
            OnGamesUpdate();
    }
    #endregion


    //===========================================//
    #region Settings
    public Game gameSelected { get; set; } = new Game();
    public List<Game> gamesList = new List<Game>();
    public bool AutoGameRunning { get; set; } = false;
    public string localPlayerName { get; set; } = "localPlayer";
    public string remotePlayerName { get; set; } = "remotePlayer";
    #endregion


    //===========================================//
    #region Initialization & Game Config
    public void ParseGames()
    {
        string ini = "Configs/Games.ini";

        if (!File.Exists(ini))
        {
            return;
        }
        string[] lines = File.ReadAllLines(ini);

        foreach (string line in lines)
        {
            if (line.StartsWith("["))
            {
                gamesList.Add(new Game { Name = line.Replace("[", "").Replace("]", ""), Description = new List<Game.DescriptionLine>() });
                Console.WriteLine("GS | Adding Game " + line.Replace("[", "").Replace("]", ""));
            }
            else
            {
                string editedLine = line;
                Typo level = Typo.body1;

                if (editedLine.StartsWith("\t"))
                {
                    editedLine = editedLine.Substring(1);
                }

                if (editedLine.Contains("H= "))
                {
                    editedLine = editedLine.Replace("H= ", "");
                    level = Typo.h5;
                }
                else if (editedLine.Contains("H== "))
                {
                    editedLine = editedLine.Replace("H== ", "");
                    level = Typo.h6;
                }

                if (editedLine.Contains("AutoGame"))
                {
                    gamesList.Last().AutoGameCapable = true;
                }

                if (editedLine.Contains("=ReadsLogs"))
                {
                    gamesList.Last().AutoGameReadsLogs = true;
                    continue;
                }

                editedLine = editedLine.Replace("\t", " ");
                editedLine = editedLine.Replace("*", " •");

                gamesList.Last().Description.Add(new Game.DescriptionLine { typo = level, text = editedLine });
            }
        }

        gameSelected = gamesList.First();

        Game firstGame = gamesList.First();
        gamesList = gamesList.OrderBy(x => x.Name).ToList();

        gamesList.Remove(firstGame);
        gamesList.Insert(0, firstGame);

        Update();
    }
    public void ChangeGame(Game game)
    {
        gameSelected = game;
        Update();
    }
    public void ChangeNames(string local, string remote)
    {
        localPlayerName = local;
        remotePlayerName = remote;
        Update();
    }
    #endregion


    //===========================================//
    #region AutoGames
    public void AG_Start()
    {
        AutoGameRunning = true;
        Update();
    }

    public void AG_Stop()
    {
        AutoGameRunning = false;
        Update();
    }
    #endregion


    /*
        #region Game Automation
        Thread logWatcherThread = null;
        public static bool isAutoGameEnabled = false;
        public static string logFilePath = "";

        private static MethodInfo methodAutoGameStartup = null;
        private static MethodInfo methodAutoGameHandleLogs = null;

        List<string> logIgnoreList = new List<string>() {
                "[Behaviour]",
                "Found SDK2",
                "Found SDK3",
                "[Network Processing]",
                "[ITEM ASSIGNMENT POOL]",
                "[GC]",
                "[EOSManager]",
                "[Always]",
                "[UdonBehaviour]",
                "Detection :",
                "Updating avatar",
                "Measure Human",
                "[AssetBundleDownloadManager]",
                "Removing animation",
                "BoxColliders",
                "[API]",
                "Saving Avatar",
                "Avatar Asset",
                "avatar cloning",

                //TON Stuff to ignore
                "Loaded the mysterious triangle",
                "OBJECT POOL PlayerHead",
                "Clearing Items",
                "Enrage State",
                "[START]",
                "stuck! help!",
                "Hit -"
            };

        private static void FindMostRecentLogFilePath()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "..", "LocalLow", "VRChat", "VRChat");

                string[] files = Directory.GetFiles(path, "output_log*.txt");

                string mostRecentFile = "";
                foreach (string file in files)
                {
                    if (file.CompareTo(mostRecentFile) > 0)
                    {
                        mostRecentFile = file;
                    }
                }

                //Write the most recent file name to the console
                string justFileName = Path.GetFileName(mostRecentFile);

                //LogService.AddLog("Found (" + justFileName + ")", CC.Info);

                logFilePath = mostRecentFile;
            }
            catch (Exception e)
            {
                //writeConsoleUI(e.Message, CC.Failure);
                logFilePath = "";
            }
        }

        private async void StartLogWatcher()
        {
            //UpdateAutoGameMethods();
            FindMostRecentLogFilePath();

            await Task.Delay(500);
            try
            {
                logWatcherThread = new Thread(async () => await WatchLogFileAsync(logFilePath));
                logWatcherThread.Start();
                isAutoGameEnabled = true;
            }
            catch (Exception ex)
            {
                isAutoGameEnabled = false;
            }
        }

        private async Task WatchLogFileAsync(string logFilePath)
        {
            using (FileStream fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BufferedStream bs = new BufferedStream(fs))
            using (StreamReader reader = new StreamReader(bs))
            {
                // Catch up to the most recent line in the log file
                while (!reader.EndOfStream)
                {
                    await reader.ReadLineAsync();
                    // No need to process this line; just skip to the end
                }

                //writeConsoleUI($"Reading log files for {selectedGame}", CC.Success);

                while (isAutoGameEnabled)
                {
                    string line = await reader.ReadLineAsync();

                    if (line == null || line.Length < 34 || !line.Substring(20, 3).Contains("Log") || logIgnoreList.Any(line.Contains))
                    {
                        await Task.Delay(10);
                        continue;
                    }

                    string logEntry = line.Substring(34);
                    Console.WriteLine(logEntry);

                    await Task.Delay(50);

                    methodAutoGameHandleLogs.Invoke(null, new object[] { logEntry });

                }
            }
        }

        private void StopLogWatcher()
        {
            logWatcherThread = null;
            isAutoGameEnabled = false;
            //writeConsoleUI("Stopped reading log files.", CC.Warning);

            //groupBoxAutoGames.Controls.Clear();
        }
        #endregion

    */
}
