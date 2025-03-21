using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ZeniControlSuite.Services;

public class Service_Points : IHostedService
{
    private readonly Service_Logs LogService;
    public Service_Points(Service_Logs serviceLogs) { LogService = serviceLogs; }
    private void Log(string message, Severity severity)
    {
        LogService.AddLog("Service_Points", "System", message, severity, Variant.Outlined);
    }

    //===========================================//
    #region HostedService Stuff 
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
    #endregion


    //===========================================//
    #region Settings
    public string pointsDisplay { get; private set; } = "0";
    public double pointsTruncated { get; private set; } = 0;
    public double pointsTotal { get; private set; } = 0;
    public double pointsWhole { get; private set; } = 0;
    public double pointAddStreak { get; private set; } = 0;
    public double pointsPartial { get; private set; } = 0;
    public double pointsPartialFlipped { get; private set; } = 100;
    #endregion

    //==
    #region helpers
    public string GetSignText(double points)
    {
        return points > 0 ? "Added +" : "Removed ";
    }
    #endregion


    //===========================================//
    #region Point Controls
    public void UpdatePoints(double points)
    {
        if (points == 0)
        {
            InvokePointsUpdate();
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
                pointsWhole += 1;
            }
            else
            {
                pointsWhole -= 1;
            }
            pointsTotal = pointsWhole;
        }

        //if the points are negative, flip the range of 0 and 100 
        if (pointsTotal < 0)
        {
            pointsPartialFlipped = 100 - pointsPartial;
        }

        //FractionalScore(pointsTotal);
        pointsTruncated = Math.Truncate(pointsTotal * 100) / 100;
        pointsDisplay =  $"{pointsTruncated}✦";
        //PointsDisplayCalc();

        InvokePointsUpdate();
    }

    public void PointsDisplayCalc()
    {
        double remainder = pointsTruncated;

		int wholeNumber = 0;
        int denominator = 1;

        wholeNumber = (int)Math.Floor(remainder);
        remainder -= wholeNumber;

        if (remainder == 0.0)
        {
            denominator = 1;
        }
        else if (remainder % 0.25 < 0.01)
        {
            remainder = (int)(1 * Math.Floor(remainder / 0.25));
            denominator = 4;
        }
        else if (remainder % 0.33 < 0.01)
        {
            remainder = (int)(1 * Math.Floor(remainder / 0.33));
            denominator = 3;
        }

        if (denominator == 1)
        {
            pointsDisplay = $"{wholeNumber}⭐";
        }
        else
        {
            pointsDisplay = $"{wholeNumber} & {remainder}/{denominator}⭐";
        }
    }

    public void InvokePointsUpdate()
    {
        if (OnPointsUpdate != null)
            OnPointsUpdate.Invoke();
    }
    #endregion

}