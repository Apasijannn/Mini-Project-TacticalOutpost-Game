using UnityEngine;

// Komponen penanda environment Tactical Outpost.
// Sengaja diberi namespace Outpost.Env dan prefix "Env" agar tidak bentrok dengan script AI tim (CoverPoint, SpawnPoint, dst).
namespace Outpost.Env
{
    public enum CoverHeight { Low, High }
    public enum LaneId { None, North, Main, South, Base, West, East }

    public enum EnvCategory
    {
        Ground, Building, Container, FuelTank, Barrel, Crate, Pallet, Pipe, Fence, Wall,
        Sandbag, Barrier, Vehicle, Tower, Light, Generator, Helipad, Rubble, Other
    }

}
