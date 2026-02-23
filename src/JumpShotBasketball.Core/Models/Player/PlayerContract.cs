using JumpShotBasketball.Core.Constants;

namespace JumpShotBasketball.Core.Models.Player;

/// <summary>
/// Player contract and free agency information.
/// Maps to CPlayer's m_contract_salaries, m_is_free_agent, m_seeking, etc.
/// </summary>
public class PlayerContract
{
    public int SalaryId { get; set; }
    public int CurrentTeam { get; set; }
    public int PreviousTeam { get; set; }
    public int YearsOnTeam { get; set; }
    public int YearsOfService { get; set; }
    public int ContractYears { get; set; }
    public int TotalSalary { get; set; }
    public int RemainingSalary { get; set; }
    public int TotalSalarySeeking { get; set; }
    public int CurrentContractYear { get; set; }
    public int CurrentYearSalary { get; set; }
    public int ContractPointer { get; set; }
    public int BestContract { get; set; }
    public bool IsFreeAgent { get; set; }
    public bool IsRookie { get; set; }
    public bool IsBirdPlayer { get; set; }
    public bool JustSigned { get; set; }
    public bool EndSeasonFreeAgent { get; set; }
    public bool Signed { get; set; }
    public int StopNegotiating { get; set; }
    public double AcceptFactor { get; set; }

    // Team paying (for waived players)
    public int TeamPaying { get; set; }
    public string TeamPayingName { get; set; } = string.Empty;

    // Salary arrays: year-by-year salary for current contract
    public int[] ContractSalaries { get; set; } = new int[LeagueConstants.MaxContractYears];

    // New contract being negotiated
    public int[] NewContractSalaries { get; set; } = new int[LeagueConstants.MaxContractYears];

    // Qualifying offer
    public int QualifyingOfferYears { get; set; }
    public int[] QualifyingSalaries { get; set; } = new int[LeagueConstants.MaxContractYears];

    // Free agency: which teams are seeking this player
    public int[] Seeking { get; set; } = new int[LeagueConstants.MaxTeams + 1];

    // Teams making this player their top choice
    public bool[] TopOffer { get; set; } = new bool[LeagueConstants.MaxTeams + 1];

    // Years offered by each team
    public int[] YearOffer { get; set; } = new int[LeagueConstants.MaxTeams + 1];

    // Year-by-year salaries offered by each team [teamIndex][year]
    public int[][] SalaryOffer { get; set; } = CreateSalaryOfferArray();

    // Total contract amount by team
    public int[] TotalSalaryOffer { get; set; } = new int[LeagueConstants.MaxTeams + 1];

    // Offer slot per team
    public int[] OfferSlot { get; set; } = new int[LeagueConstants.MaxTeams + 1];

    // Preference factors
    public int CoachFactor { get; set; }
    public int SecurityFactor { get; set; }
    public int LoyaltyFactor { get; set; }
    public int WinningFactor { get; set; }
    public int PlayingTimeFactor { get; set; }
    public int TraditionFactor { get; set; }

    private static int[][] CreateSalaryOfferArray()
    {
        var array = new int[LeagueConstants.MaxTeams + 1][];
        for (int i = 0; i < array.Length; i++)
            array[i] = new int[LeagueConstants.MaxContractYears];
        return array;
    }
}
