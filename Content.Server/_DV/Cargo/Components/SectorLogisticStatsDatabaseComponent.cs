using Content.Server._DV.Cargo.Systems;
using Content.Shared.Cargo;
using Content.Shared.CartridgeLoader.Cartridges;

namespace Content.Server._DV.Cargo.Components;

/// <summary>
/// Tracks all mail statistics for mail activity in the sector.
/// </summary>
[RegisterComponent, Access(typeof(SharedCargoSystem), typeof(LogisticStatsSystem))] // HS: Added LogisticStatsSystem
public sealed partial class SectorLogisticStatsComponent : Component // Frontier: Station->Sector
{
    [DataField]
    public MailStats Metrics { get; set; }
}
