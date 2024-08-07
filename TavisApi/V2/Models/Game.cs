using TavisApi.V2.Bcm.Models;
using TavisApi.V2.TrueAchievements.Models;

namespace TavisApi.V2.Models;

public class Game {
	public int Id { get; set; } = 0;
	public int TrueAchievementId { get; set; } = 0;
	public string? Url { get; set; }
	public string? Title { get; set; }
	public int? TrueAchievement { get; set; }
	public int? Gamerscore { get; set; }
	public int? AchievementCount { get; set; }
	public string? Publisher { get; set; }
	public string? Developer { get; set; }
	public DateTime? ReleaseDate { get; set; }
	public int? GamersWithGame { get; set; }
	public int? GamersCompleted { get; set; }
	public double? BaseCompletionEstimate { get; set; }
	public double? SiteRatio { get; set; }
	public double? SiteRating { get; set; }
	public bool Unobtainables { get; set; } = false;
	public DateTime? ServerClosure { get; set; }
	public double? InstallSize { get; set; }
	public double? FullCompletionEstimate { get; set; }
	public bool ManuallyScored { get; set; }


	public FeatureList? FeatureList { get; set; }
	public IList<GameGenre>? GameGenres { get; set; }
	public IList<PlayerTopGenre>? PlayerTopGenres { get; set; }
	public ICollection<BcmPlayerCompletionHistory>? PlayerCompletionHistories { get; set; }
	public ICollection<BcmCompletionHistory>? BcmCompletionHistories { get; set; }
}
