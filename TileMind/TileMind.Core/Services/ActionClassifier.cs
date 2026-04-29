using TileMind.Common.Models;

namespace TileMind.Core.Services;

/// <summary>
/// 动作分类器：根据各玩家的状态增量变化推断发生的游戏动作。
/// </summary>
public class ActionClassifier
{
    /// <summary>
    /// 根据各玩家帧间增量分类动作。
    /// </summary>
    public List<MahjongAction> Classify(Dictionary<SeatPosition, PlayerFrameDelta> deltas)
    {
        var actions = new List<MahjongAction>();

        // Pass 1: 逐玩家分类
        foreach (var (seat, delta) in deltas)
        {
            if (delta.HasNoChange) continue;
            actions.AddRange(ClassifyPlayerActions(seat, delta));
        }

        // Pass 2: 跨玩家关联（吃碰杠的来源）
        EnrichMeldSources(actions, deltas);

        return actions;
    }

    private List<MahjongAction> ClassifyPlayerActions(SeatPosition seat, PlayerFrameDelta delta)
    {
        var actions = new List<MahjongAction>();

        int handDiff = delta.CurrentHandCount - delta.PreviousHandCount;

        // 1. 摸牌: Hand +1, 无其他变化
        if (handDiff == 1 && delta.PondAdded.Count == 0 && delta.MeldsAdded.Count == 0)
        {
            actions.Add(new MahjongAction
            {
                ActionType = ActionType.Draw,
                Player = seat,
                Tiles = delta.HandAdded.ToList()
            });

            if (handDiff == 1 && delta.PondAdded.Count == 1)
            {
                // 手切或摸切：同时有摸牌和出牌
                actions.Add(new MahjongAction
                {
                    ActionType = ActionType.Draw,
                    Player = seat,
                    Tiles = delta.HandAdded.ToList()
                });
                actions.Add(new MahjongAction
                {
                    ActionType = ActionType.Discard,
                    Player = seat,
                    Tiles = delta.PondAdded.ToList()
                });
            }

            return actions;
        }

        // 2. 出牌: Hand -1, Pond +1
        if (handDiff == -1 && delta.PondAdded.Count == 1 && delta.MeldsAdded.Count == 0)
        {
            actions.Add(new MahjongAction
            {
                ActionType = ActionType.Discard,
                Player = seat,
                Tiles = delta.PondAdded.ToList()
            });
            return actions;
        }

        // 3. 出牌 + 摸牌 在同一帧内（快速操作）
        if (handDiff == 0 && delta.PondAdded.Count == 1 && delta.MeldsAdded.Count == 0)
        {
            actions.Add(new MahjongAction
            {
                ActionType = ActionType.Discard,
                Player = seat,
                Tiles = delta.PondAdded.ToList()
            });
            return actions;
        }

        // 4. Kakan (加杠)：已有碰升级为杠
        if (delta.UpgradedMeldIndex >= 0)
        {
            actions.Add(new MahjongAction
            {
                ActionType = ActionType.Kakan,
                Player = seat,
                Tiles = delta.HandRemoved.ToList()
            });
            return actions;
        }

        // 5. 新副露: Hand -N, Meld +1
        foreach (var meld in delta.MeldsAdded)
        {
            var actionType = DetermineMeldActionType(meld, handDiff);
            actions.Add(new MahjongAction
            {
                ActionType = actionType,
                Player = seat,
                Tiles = meld.Tiles.ToList()
            });
        }

        // 6. 和牌: 手牌清空
        if (delta.PreviousHandCount > 0 && delta.CurrentHandCount == 0 && delta.MeldsAdded.Count == 0)
        {
            actions.Add(new MahjongAction
            {
                ActionType = ActionType.Tsumo,
                Player = seat,
                Tiles = new()
            });
        }

        return actions;
    }

    private static ActionType DetermineMeldActionType(MeldRecord meld, int handDiff)
    {
        var tiles = meld.Tiles;

        if (tiles.Count == 4)
        {
            // hand -4 且所有 tile 在 meld 中 → 暗杠
            if (handDiff == -4) return ActionType.Ankan;
            // hand -3 → 明杠
            return ActionType.Kan;
        }

        if (tiles.Count == 3)
        {
            var tileTypes = tiles.Select(t => NormalizeTileType(t.TileType)).ToList();

            if (IsChi(tileTypes))
                return ActionType.Chi;

            return ActionType.Pon;
        }

        return ActionType.Pon;
    }

    /// <summary>
    /// 跨玩家关联：为 Chi/Pon/Kan 找到被吃/碰/杠的弃牌来源。
    /// </summary>
    private static void EnrichMeldSources(
        List<MahjongAction> actions, Dictionary<SeatPosition, PlayerFrameDelta> deltas)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            if (action.ActionType is not (ActionType.Chi or ActionType.Pon or ActionType.Kan))
                continue;

            var recentDiscards = actions
                .Where(a => a.ActionType == ActionType.Discard && a.Player != action.Player)
                .OrderByDescending(a => a.Timestamp)
                .ToList();

            foreach (var discard in recentDiscards)
            {
                var discardTile = discard.Tiles.FirstOrDefault();
                if (discardTile == null) continue;

                bool tileInMeld = action.Tiles.Any(t =>
                    NormalizeTileType(t.TileType) == NormalizeTileType(discardTile.TileType));

                if (tileInMeld)
                {
                    actions[i] = action with { RelatedPlayer = discard.Player };
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 判断三枚牌是否构成顺子（Chi）。
    /// </summary>
    internal static bool IsChi(List<int> normalizedTypes)
    {
        if (normalizedTypes.Count != 3) return false;

        normalizedTypes.Sort();

        // 检查同花色
        int suit0 = normalizedTypes[0] / 10;
        int suit1 = normalizedTypes[1] / 10;
        int suit2 = normalizedTypes[2] / 10;

        if (suit0 != suit1 || suit1 != suit2) return false;
        if (suit0 == 3) return false; // 字牌不能构成顺子

        // 检查连续数字
        int n0 = normalizedTypes[0] % 10;
        int n1 = normalizedTypes[1] % 10;
        int n2 = normalizedTypes[2] % 10;

        return n1 == n0 + 1 && n2 == n1 + 1;
    }

    /// <summary>
    /// 标准化 TileType 用于比较：M0/P0/S0 映射为 M5/P5/S5。
    /// </summary>
    internal static int NormalizeTileType(TileType tileType)
    {
        int val = (int)tileType;
        int suit = val / 10;
        int num = val % 10;

        if (num == 0 && suit < 3)
            return suit * 10 + 5; // 赤牌映射为对应数字 5

        return val;
    }
}

/// <summary>
/// 单个玩家的一帧状态增量。
/// </summary>
public class PlayerFrameDelta
{
    public SeatPosition Seat { get; set; }
    public List<TrackedTile> HandAdded { get; set; } = new();
    public List<TrackedTile> HandRemoved { get; set; } = new();
    public List<TrackedTile> PondAdded { get; set; } = new();
    public List<TrackedTile> PondRemoved { get; set; } = new();
    public List<MeldRecord> MeldsAdded { get; set; } = new();
    public int PreviousHandCount { get; set; }
    public int CurrentHandCount { get; set; }
    public int PreviousPondCount { get; set; }
    public int CurrentPondCount { get; set; }
    public int UpgradedMeldIndex { get; set; } = -1;

    public bool HasNoChange =>
        HandAdded.Count == 0 && HandRemoved.Count == 0 &&
        PondAdded.Count == 0 && PondRemoved.Count == 0 &&
        MeldsAdded.Count == 0 && UpgradedMeldIndex < 0;
}
