using System.Diagnostics;
using MudBlazor;
namespace ZeniControlSuite.Components;

public class GamesService : IHostedService
{
    public delegate void GamesUpdate();
    public event GamesUpdate? OnGamesUpdate;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            ParseGames();
        }
        catch (Exception e)
        {
            gamesList.Add(new Game { Name = "Error", Description = new List<Game.Line> { new Game.Line { typo = Typo.body1, text = "Games.ini parsing failed: " + e.Message } } });
            Console.WriteLine("Games.ini parsing failed: " + e.Message);
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

    public Game gameSelected { get; set; }
    public List<Game> gamesList = new List<Game>();
    public bool AutoGameRunning { get; set; } = false;
    public string localPlayerName { get; set; } = "localPlayer";
    public string remotePlayerName { get; set; } = "remotePlayer";

    //Parse Games.ini file for games
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
                gamesList.Add(new Game { Name = line.Replace("[", "").Replace("]", ""), Description = new List<Game.Line>() });
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

                editedLine = editedLine.Replace("\t", " ");
                editedLine = editedLine.Replace("*", " •");

                gamesList.Last().Description.Add(new Game.Line { typo = level, text = editedLine });
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

}
