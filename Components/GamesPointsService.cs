namespace ZeniControlSuite.Components;

public class GamesPointsService : IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	public delegate void PointsUpdate();
    public event PointsUpdate? OnPointsUpdate;
    
    public double pointsTotal { get; private set; } = 0;
    public double pointsWhole { get; private set; } = 0;
    public double pointAddStreak { get; private set; } = 0;
    public double pointsPartial { get; private set; } = 0;
    public double pointsPartialFlipped { get; private set; } = 100;
    
    public void UpdatePoints(double points)
    {
        pointsTotal += points;
        pointAddStreak = Math.Clamp(pointAddStreak+points, -1, 1);
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
            
        if(OnPointsUpdate != null)
            OnPointsUpdate();
    }

    //Zeni Cursed Math
    public static string DecimalToFraction(double points)
    {
        int wholeNumber = 0;
        int denominator = 1;

        if (points >= 1.0)
        {
            wholeNumber = (int)Math.Floor(points);
            points -= wholeNumber;
        }

        if (points == 0.0)
        {
            return wholeNumber.ToString();
        }

        if (points % 0.125 < 0.01)
        {
            denominator = 8;
        }
        else if (points % 0.25 < 0.01)
        {
            denominator = 4;
        }
        else if (points % 0.33 < 0.01)
        {
            denominator = 3;
        }

        string fraction = $"{Math.Floor(points * denominator)}/{denominator}";

        if (denominator != 1)
        {
            if (wholeNumber > 0)
            {
                return $"{wholeNumber} and {fraction}";
            }
            else
            {
                return $"{fraction}";
            }
        }
        else
        {
            return points.ToString();
        }
    }

    public string gameSelected = "None";
	public string[] gameList = [
		"None",
		"Placeholder A",
		"Placeholder B",
		"Placeholder C"
	];



}