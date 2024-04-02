namespace ZeniControlSuite.Components;

public class GamesPointsService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            ParseGames();
        }
        catch (Exception e)
        {
            gamesList.Add(new Game { Name = "None", Description = new List<string> { "No games found." } });
            Console.WriteLine(e.Message);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #region Points

    public delegate void GamesPointsUpdate();
    public event GamesPointsUpdate? OnGamesPointsUpdate;
    public string pointsDisplay { get; private set; } = "0";
    public double pointsTotal { get; private set; } = 0;
    public double pointsWhole { get; private set; } = 0;
    public double pointAddStreak { get; private set; } = 0;
    public double pointsPartial { get; private set; } = 0;
    public double pointsPartialFlipped { get; private set; } = 100;

    public void Update()
    {
        if (OnGamesPointsUpdate != null)
            OnGamesPointsUpdate();
    }

    public void UpdatePoints(double points)
    {
        pointsTotal += points;
        pointAddStreak = Math.Clamp(pointAddStreak + points, -1, 1);
        pointsWhole = Math.Truncate(pointsTotal);
        pointsPartial = Math.Abs((pointsTotal - pointsWhole) * 100);

        if (pointsPartial < 5)
        {
            pointsPartial = 0;
            pointsTotal = pointsWhole;
        }

        if (pointsPartial > 95)
        {
            pointsPartial = 0;
            if (pointsTotal > 0)
            {
                pointsWhole = pointsWhole + 1;
            }
            else
            {
                pointsWhole = pointsWhole - 1;
            }
            pointsTotal = pointsWhole;
        }

        //if the points are negative, flip the range of 0 and 100 
        if (pointsTotal < 0)
        {
            pointsPartialFlipped = 100 - pointsPartial;
        }

        FractionalScore(pointsTotal);

        Update();
    }

    public void FractionalScore(double value)
    {
        //cut off everything after the hundreths place, do not round.
        value = Math.Truncate(value * 100) / 100;

        int wholeNumber = 0;
        int denominator = 1;
        double rounded = 0.0;

        wholeNumber = (int)Math.Floor(value);
        value -= wholeNumber;

        if (value == 0.0)
        {
            denominator = 1;
        }
        else if (value % 0.25 < 0.01)
        {
            value = (int)(1 * Math.Floor(value / 0.25));
            denominator = 4;
        }
        else if (value % 0.33 < 0.01)
        {
            value = (int)(1 * Math.Floor(value / 0.33));
            denominator = 3;
        }

        if (denominator == 1)
        {
            pointsDisplay = $"{wholeNumber}";
        }
        else
        {
            pointsDisplay = $"{wholeNumber} & {value}/{denominator}";
        }


    }

    #endregion

    #region Games
    public Game gameSelected { get; set; }
    public List<Game> gamesList = new List<Game>();
    public bool AutoGame { get; set; } = true;

    public void UpdateGame(Game game)
    {
        gameSelected = game;

        Update();
    }

    //Parse Games.ini file for games
    public void ParseGames()
    {
        string ini = "Configs/Games.ini";

        if (!File.Exists(ini))
        {
            gamesList.Add(new Game { Name = "None", Description = new List<string> { "No games found." } });
            return;
        }
        string[] lines = File.ReadAllLines(ini);
        
        foreach (string line in lines)
        {
            //if the line starts with [ then it's a new game
            if (line.StartsWith("["))
            {
                gamesList.Add(new Game { Name = line.Replace("[", "").Replace("]", ""), Description = new List<string>() });
            }
            else
            {
                gamesList.Last().Description.Add(line);
            }
        }

        gameSelected = gamesList.First();
    }

    #endregion
}