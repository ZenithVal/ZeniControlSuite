﻿using MudBlazor;
namespace ZeniControlSuite.Models;
public class Game
{
    public string Name { get; set; }
    public List<DescriptionLine> Description { get; set; } = new List<DescriptionLine>();
    public List<MDLink> MDLinks { get; set; } = new List<MDLink>();

    public class MDLink
    {
		public string text { get; set; } = "";
		public string url { get; set; } = "";
	}

    public class DescriptionLine
    {
        public Typo typo { get; set; } = Typo.body1;
        public string text { get; set; } = "";
    }

    public bool AutoGameCapable { get; set; } = false;
    public bool AutoGameReadsLogs { get; set; } = false;
}

public class GameType_TerrorsOfNowhere
{

}

public class GameType_Billiards
{

}

