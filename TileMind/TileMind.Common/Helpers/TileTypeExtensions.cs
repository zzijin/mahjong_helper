using System;
using System.Collections.Generic;
using System.Text;
using TileMind.Common.Models;

namespace TileMind.Common.Helpers
{
    public static class TileTypeExtensions
    {
        extension(TileType tileType)
        {
            public string GetTileName()
            {
                switch(tileType) {
                    case TileType.M0: return "M0";
                    case TileType.M1: return "M1";
                    case TileType.M2: return "M2";
                    case TileType.M3: return "M3";
                    case TileType.M4: return "M4";
                    case TileType.M5: return "M5";
                    case TileType.M6: return "M6";
                    case TileType.M7: return "M7";
                    case TileType.M8: return "M8";
                    case TileType.M9: return "M9";
                    case TileType.P0: return "P0";
                    case TileType.P1: return "P1";
                    case TileType.P2: return "P2";
                    case TileType.P3: return "P3";
                    case TileType.P4: return "P4";
                    case TileType.P5: return "P5";
                    case TileType.P6: return "P6";
                    case TileType.P7: return "P7";
                    case TileType.P8: return "P8";
                    case TileType.P9: return "P9";
                    case TileType.S0: return "S0";
                    case TileType.S1: return "S1";
                    case TileType.S2: return "S2";
                    case TileType.S3: return "S3";
                    case TileType.S4: return "S4";
                    case TileType.S5: return "S5";
                    case TileType.S6: return "S6";
                    case TileType.S7: return "S7";
                    case TileType.S8: return "S8";
                    case TileType.S9: return "S9";
                    case TileType.Z1: return "Z1";
                    case TileType.Z2: return "Z2";
                    case TileType.Z3: return "Z3";
                    case TileType.Z4: return "Z4";
                    case TileType.Z5: return "Z5";
                    case TileType.Z6: return "Z6";
                    case TileType.Z7: return "Z7";
                    default: return "UN";
                }
            }
        }
    }
}
