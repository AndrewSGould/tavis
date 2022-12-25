using Ardalis.SmartEnum;

namespace Tavis.Models;

public sealed class SyncProfileList : SmartEnum<SyncProfileList> {
  public static readonly SyncProfileList Full = new SyncProfileList(0, "Full");
  public static readonly SyncProfileList Custom = new SyncProfileList(1, "Custom");
  public static readonly SyncProfileList LastMonthsCompleted = new SyncProfileList(2, "LastMonthsCompleted");
  

  private SyncProfileList(int id, string name) : base(name, id)
  {
  }
}