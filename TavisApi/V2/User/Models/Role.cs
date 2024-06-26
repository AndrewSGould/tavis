using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TavisApi.V2.Users;

public class Role {
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public long Id { get; set; }
	public ulong DiscordId { get; set; }
	public string RoleName { get; set; } = "";
	public List<UserRole> UserRoles { get; } = new();
}
