using RiichiSharp;
using RiichiSharp.Tenpai;
using TileMind.Common.Models;

namespace TileMind.Algorithm;

/// <summary>
/// 牌型分析服务：桥接 TileMind 的 AnalyzedFrame 到 RiichiSharp 麻将算法。
/// 输出向听数、听牌分析、打牌推荐、胡牌选项。
/// </summary>
public class TileAnalysisService
{
    /// <summary>
    /// 基于单帧静态分析结果，对当前本家手牌做完整牌型分析。
    /// 场风/自风暂用默认值（东/东），后续接入 InfoArea 解析后修正。
    /// </summary>
    public TileAnalysisResult Analyze(AnalyzedFrame analysis)
    {
        var self = analysis.Players.GetValueOrDefault(SeatPosition.Self);
        if (self == null)
            return new TileAnalysisResult { Shanten = 6 };

        var result = new TileAnalysisResult();

        // 1. 构建牌谱字符串
        string handStr = HandStringBuilder.BuildHandString(self.HandTiles);
        int calledMelds = HandStringBuilder.CountCalledMelds(self.Melds);
        string visibleStr = HandStringBuilder.BuildVisibleString(analysis);
        int akaCount = HandStringBuilder.CountAkaDora(self.HandTiles);

        // 2. 向听数计算
        var shantenResult = MahjongEngine.Shanten(handStr, calledMelds);
        result.Shanten = shantenResult.Shanten;

        // 3. 牌山剩余统计
        result.RemainingCounts = CalcRemainingCounts(analysis, handStr);

        // 4. 听牌 / 未听牌分支
        if (result.IsTenpai)
            FillWinOptions(result, self, handStr, calledMelds, visibleStr, akaCount);
        else
            FillDiscardOptions(result, handStr, calledMelds);

        return result;
    }

    // ─────────────── 听牌分支：计算每种等牌的胡牌点数 ───────────────

    private static void FillWinOptions(
        TileAnalysisResult result, PlayerFrameAnalysis self,
        string handStr, int calledMelds, string visibleStr, int akaCount)
    {
        var tiles = RiichiSharp.Parse.TenhouParser.Parse(handStr);
        if (tiles == null) return;

        var visible = RiichiSharp.Parse.TenhouParser.Parse(visibleStr);
        var tenpai = TenpaiCalculator.Calculate(tiles.Value, calledMelds, visible);

        foreach (var wait in tenpai.Waits)
        {
            var winTile = TileTypeMapper.ToTileMind(wait);
            var rsTile = TileTypeMapper.ToRiichiSharp(winTile);

            // 构建胡牌时的完整手牌字符串
            string scoringHand = HandStringBuilder.BuildScoringHandString(self) +
                                 rsTile.Number + SuitChar(rsTile.Suit);

            var ctx = new GameContext
            {
                WinType = WinType.Tsumo,
                WinningTile = rsTile,
                RoundWind = RiichiSharp.Tiles.Tile.East,
                SeatWind = RiichiSharp.Tiles.Tile.East,
                AkaCount = akaCount,
            };

            try
            {
                var score = MahjongEngine.Score(scoringHand, ctx);
                int remaining = result.RemainingCounts.GetValueOrDefault(winTile, 0);
                result.WinOptions.Add(new WinOption
                {
                    WinTile = winTile,
                    Remaining = remaining,
                    Han = score.Han,
                    Fu = score.FuTotal,
                    Points = score.Points,
                    YakuNames = score.YakuList?.Select(y => y.ToString()).ToList() ?? new()
                });
            }
            catch
            {
                // Score 计算失败时至少记录等牌和剩余张数
                result.WinOptions.Add(new WinOption
                {
                    WinTile = winTile,
                    Remaining = result.RemainingCounts.GetValueOrDefault(winTile, 0)
                });
            }
        }
    }

    // ─────────────── 未听牌分支：打牌效率分析 ───────────────

    private static void FillDiscardOptions(
        TileAnalysisResult result, string handStr, int calledMelds)
    {
        try
        {
            var discards = MahjongEngine.EffectiveDiscards(handStr, calledMelds);
            foreach (var d in discards)
            {
                result.DiscardOptions.Add(new DiscardOption
                {
                    DiscardTile = TileTypeMapper.ToTileMind(d.Discarded),
                    ShantenAfter = d.ShantenAfter,
                    UniqueUkeire = d.UniqueUkeire,
                    TotalUkeire = d.TotalUkeire,
                    UkeireTiles = d.UkeireTiles?
                        .Select(u => new UkeireTile
                        {
                            Tile = TileTypeMapper.ToTileMind(u.Tile),
                            Available = u.Available
                        }).ToList() ?? new()
                });
            }
        }
        catch
        {
            // 计算失败时 DiscardOptions 留空
        }
    }

    // ─────────────── 牌剩余统计 ───────────────

    private static Dictionary<TileType, int> CalcRemainingCounts(
        AnalyzedFrame analysis, string selfHandStr)
    {
        var known = new int[34];

        // 本家手牌
        CountTiles(known, selfHandStr);

        // 本家副露
        if (analysis.Players.TryGetValue(SeatPosition.Self, out var self))
        {
            foreach (var meld in self.Melds)
            {
                foreach (var det in meld.Tiles)
                    known[(int)TileTypeMapper.ToRiichiSharp(det.TileType).Id]++;
            }
        }

        // 所有弃牌 + 其他玩家副露 + 宝牌指示牌
        foreach (var (_, pondDets) in analysis.DiscardPondDetections)
        {
            foreach (var det in pondDets)
                known[(int)TileTypeMapper.ToRiichiSharp(det.TileType).Id]++;
        }

        foreach (var (seat, player) in analysis.Players)
        {
            if (seat == SeatPosition.Self) continue;
            foreach (var meld in player.Melds)
            {
                foreach (var det in meld.Tiles)
                    known[(int)TileTypeMapper.ToRiichiSharp(det.TileType).Id]++;
            }
        }

        foreach (var det in analysis.DoraIndicatorDetections)
            known[(int)TileTypeMapper.ToRiichiSharp(det.TileType).Id]++;

        // 每种牌最多 4 张
        var result = new Dictionary<TileType, int>();
        for (int i = 0; i < 34; i++)
            result[TileTypeMapper.ToTileMind(new RiichiSharp.Tiles.Tile((byte)i))] = Math.Max(0, 4 - known[i]);

        return result;
    }

    private static void CountTiles(int[] known, string handStr)
    {
        var tiles = RiichiSharp.Parse.TenhouParser.Parse(handStr);
        if (tiles == null) return;
        for (int i = 0; i < 34; i++)
            known[i] += tiles.Value[i];
    }

    private static char SuitChar(RiichiSharp.Tiles.Suit suit) => suit switch
    {
        RiichiSharp.Tiles.Suit.Manzu => 'm',
        RiichiSharp.Tiles.Suit.Pinzu => 'p',
        RiichiSharp.Tiles.Suit.Souzu => 's',
        _ => 'z'
    };
}
