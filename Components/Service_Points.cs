using System.Diagnostics;
using MudBlazor;
namespace ZeniControlSuite.Components;

public class Service_Points : IHostedService
{
    public delegate void GamesPointsUpdate();
    public event GamesPointsUpdate? OnPointsUpdate;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    public void Update()
    {
        if (OnPointsUpdate != null)
            OnPointsUpdate();
    }

    public string pointsDisplay { get; private set; } = "0";

    public double pointsTruncated { get; private set; } = 0;
    public double pointsTotal { get; private set; } = 0;
    public double pointsWhole { get; private set; } = 0;
    public double pointAddStreak { get; private set; } = 0;
    public double pointsPartial { get; private set; } = 0;
    public double pointsPartialFlipped { get; private set; } = 100;

    public void UpdatePoints(double points)
    {
        if (points == 0)
        {
            Update();
            return;
        }
        
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

        //FractionalScore(pointsTotal);
        pointsTruncated = (Math.Truncate(pointsTotal * 100) / 100);

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

}