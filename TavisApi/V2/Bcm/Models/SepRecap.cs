﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TavisApi.V2.Bcm.Models;

public class SepRecap {
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public long Id { get; set; }
	public int Rank { get; set; }
	public string Gamertag { get; set; } = string.Empty;
	public int? StreakCount { get; set; }
	public string? StreakedGames { get; set; }
	public int CommunityBonus { get; set; }
	public bool Participation { get; set; } = false;
	public double TotalPoints { get; set; } = 0.0;

	public long PlayerId { get; set; }
	public BcmPlayer? BcmPlayer { get; set; }
}