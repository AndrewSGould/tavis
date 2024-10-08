using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TavisApi.V2.Bcm.Rgsc.Models;
using TavisApi.V2.Users;

namespace TavisApi.V2.Bcm.Models;

public class BcmPlayer {
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public long Id { get; set; }
	public long UserId { get; set; }
	public User? User { get; set; }
	public int TrueAchievementId { get; set; } = 0;
	public DateTime? LastSync { get; set; }
	public ICollection<BcmPlayerGame>? BcmPlayerGames { get; set; }
	public ICollection<MonthlyExclusion>? MonthlyExclusions { get; set; }
	public ICollection<BcmPlayerCompletionHistory>? BcmPlayerCompletionHistories { get; set; }
	public BcmStat? BcmStats { get; set; }
	public JanRecap? JanRecap { get; set; }
	public FebRecap? FebRecap { get; set; }
	public AprRecap? AprRecap { get; set; }
	public MayRecap? MayRecap { get; set; }
	public JunRecap? JunRecap { get; set; }
	public JulyRecap? JulyRecap { get; set; }
	public AugRecap? AugustRecap { get; set; }
	public SepRecap? SeptemberRecap { get; set; }
	public BcmMiscStat? BcmMiscStats { get; set; }
	public ICollection<BcmRgsc>? BcmRgscs { get; set; }
}
