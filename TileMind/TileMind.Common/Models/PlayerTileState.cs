using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.Common.Models
{
    public class PlayerTileState
    {
        public PlayerTileState()
        {
            HandTiles = new List<TileDetectionResult>();
            DiscardPondTiles = new List<TileDetectionResult>();
            ChiTiles = new List<TileDetectionResult>();
            PonTiles = new List<TileDetectionResult>();
            KanTiles = new List<TileDetectionResult>();
            AnkanTiles = new List<TileDetectionResult>();
        }

        //手牌状态
        public List<TileDetectionResult> HandTiles { get; }
        //牌河
        public List<TileDetectionResult> DiscardPondTiles { get; }
        //吃
        public List<TileDetectionResult> ChiTiles { get; }
        //碰
        public List<TileDetectionResult> PonTiles { get; }
        //明杠
        public List<TileDetectionResult> KanTiles { get; }
        //暗杠
        public List<TileDetectionResult> AnkanTiles { get; }
        //副露
        public IEnumerable<TileDetectionResult> MeldTiles => ChiTiles.Concat(PonTiles).Concat(KanTiles);
    }
}
