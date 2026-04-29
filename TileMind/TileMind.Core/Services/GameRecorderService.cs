using Microsoft.Extensions.Logging;
using TileMind.Common.Models;

namespace TileMind.Core.Services;

/// <summary>
/// 对局记录服务：封装 GameStateTracker，提供更高级的对局生命周期管理 API。
/// </summary>
public class GameRecorderService
{
    private readonly GameStateTracker _tracker;
    private readonly ILogger<GameRecorderService> _logger;

    private readonly List<MahjongAction> _unprocessedActions = new();

    public GameRecorderService(GameStateTracker tracker, ILogger<GameRecorderService> logger)
    {
        _tracker = tracker;
        _logger = logger;
        _tracker.OnActionDetected += OnAction;
    }

    public GameState CurrentState => _tracker.State;
    public IReadOnlyList<MahjongAction> ActionLog => _tracker.State.ActionLog;

    public event Action<MahjongAction>? OnActionRecorded;

    /// <summary>
    /// 处理一帧检测输入。
    /// </summary>
    public List<MahjongAction> ProcessFrame(FrameDetections input)
    {
        _unprocessedActions.Clear();
        var actions = _tracker.ProcessFrame(input);
        _unprocessedActions.AddRange(actions);
        return actions;
    }

    /// <summary>
    /// 开始新的一局。
    /// </summary>
    public void StartNewRound()
    {
        _tracker.Reset();
        _logger.LogInformation("新的一局开始。");
    }

    /// <summary>
    /// 获取当前帧的所有玩家手牌数量摘要。
    /// </summary>
    public Dictionary<SeatPosition, int> GetHandCounts()
    {
        var counts = new Dictionary<SeatPosition, int>();
        foreach (var (seat, player) in _tracker.State.Players)
            counts[seat] = player.HandTiles.Count;
        return counts;
    }

    /// <summary>
    /// 获取最近未处理的动作。
    /// </summary>
    public IReadOnlyList<MahjongAction> GetRecentActions()
    {
        return _unprocessedActions;
    }

    private void OnAction(MahjongAction action)
    {
        OnActionRecorded?.Invoke(action);
    }
}
