using JumpShotBasketball.Core.Enums;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Generates play-by-play commentary strings for game events.
/// Initially stubbed with basic descriptions; can be expanded later.
/// Maps to C++ CEngine's FieldGoalAttempt(), FreeThrowAttempt(), Turnover(), etc.
/// </summary>
public static class PlayByPlayGenerator
{
    public static string FieldGoalMade(string playerName, string teamName, ShotType shotType, bool putBack, bool andOne)
    {
        string shotDesc = shotType switch
        {
            ShotType.Outside => "jumper",
            ShotType.Penetration => "driving layup",
            ShotType.Inside => "post move",
            ShotType.Fastbreak => "fastbreak layup",
            _ => "shot"
        };

        if (putBack)
            return $"{playerName} puts it back in!{(andOne ? " And one!" : "")}";

        return $"{playerName} ({teamName}) hits a {shotDesc}.{(andOne ? " And one!" : "")}";
    }

    public static string FieldGoalMissed(string playerName, ShotType shotType, bool putBack)
    {
        string shotDesc = shotType switch
        {
            ShotType.Outside => "jumper",
            ShotType.Penetration => "driving layup",
            ShotType.Inside => "post move",
            _ => "shot"
        };

        if (putBack)
            return $"{playerName} puts it back... no good.";

        return $"{playerName} misses a {shotDesc}.";
    }

    public static string ThreePointMade(string playerName, string teamName, bool andOne)
    {
        return $"{playerName} ({teamName}) drains a three!{(andOne ? " And one!" : "")}";
    }

    public static string ThreePointMissed(string playerName)
    {
        return $"{playerName} misses from beyond the arc.";
    }

    public static string FreeThrow(string playerName, bool made, string ordinal)
    {
        return made
            ? $"{playerName} makes the {ordinal} free throw."
            : $"{playerName} misses the {ordinal} free throw.";
    }

    public static string PersonalFoul(string foulerName, int totalFouls, bool andOne, bool inTheAct, bool offensive)
    {
        if (offensive)
            return $"Offensive foul on {foulerName}. ({totalFouls} PF)";
        if (andOne)
            return $"Foul on {foulerName} on the made basket. ({totalFouls} PF)";
        if (inTheAct)
            return $"Shooting foul on {foulerName}. ({totalFouls} PF)";
        return $"Foul on {foulerName}. ({totalFouls} PF)";
    }

    public static string Turnover(string playerName, bool offensive)
    {
        return offensive
            ? $"Offensive foul — turnover by {playerName}."
            : $"{playerName} turns it over.";
    }

    public static string Steal(string stealerName, string victimName)
    {
        return $"{stealerName} steals it from {victimName}!";
    }

    public static string Block(string blockerName, string shooterName)
    {
        return $"{blockerName} blocks {shooterName}'s shot!";
    }

    public static string OffensiveRebound(string playerName, bool putBack)
    {
        return putBack
            ? $"{playerName} grabs the offensive rebound and goes back up!"
            : $"{playerName} grabs the offensive rebound.";
    }

    public static string DefensiveRebound(string playerName)
    {
        return $"{playerName} pulls down the defensive rebound.";
    }

    public static string TeamRebound(string teamName)
    {
        return $"{teamName} team rebound.";
    }

    public static string Assist(string assistPlayerName)
    {
        return $"(Assist: {assistPlayerName})";
    }

    public static string Fastbreak(string playerName, string teamName)
    {
        return $"{playerName} leads the fastbreak for {teamName}!";
    }

    public static string Injury(string playerName, int gamesOut)
    {
        return gamesOut > 1
            ? $"{playerName} is injured and will miss {gamesOut} games."
            : $"{playerName} is shaken up but stays in.";
    }

    public static string Timeout(string teamName, bool twentySec)
    {
        return twentySec
            ? $"20-second timeout called by {teamName}."
            : $"Timeout called by {teamName}.";
    }

    public static string EndOfQuarter(int quarter)
    {
        return quarter switch
        {
            1 => "*** End of the 1st Quarter ***",
            2 => "*** End of the 1st Half ***",
            3 => "*** End of the 3rd Quarter ***",
            _ => $"*** End of Quarter {quarter} ***"
        };
    }

    public static string EndOfGame() => "*** End of the Game ***";

    public static string JumpBall(string playerName)
    {
        return $"{playerName} wins the opening tip.";
    }

    public static string Overtime() => "*** Overtime ***";

    public static string FouledOut(string playerName)
    {
        return $"{playerName} has fouled out of the game.";
    }
}
