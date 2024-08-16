using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TavisApi.Users.Models;

namespace TavisApi.Models;

public class Player {
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public ulong Id { get; set; }
	public ulong UserId { get; set; }
	public User? User { get; set; }
	public int TrueAchievementId { get; set; } = 0;
	public DateTime? LastSync { get; set; }
	public ICollection<PlayerGame>? PlayerGames { get; set; }
}
