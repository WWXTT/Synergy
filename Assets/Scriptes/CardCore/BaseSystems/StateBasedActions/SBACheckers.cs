using System;
using System.Collections.Generic;
using System.Linq;


namespace CardCore
{
    /// <summary>
    /// 状态动作检查器接口
    /// </summary>
    public interface ISBAChecker
    {
        /// <summary>
        /// 检查并添加状态动作
        /// </summary>
        void Check(StateBasedActions sba, GameCore gameCore);
    }

    /// <summary>
    /// 玩家生命值归零检查器
    /// </summary>
    public class ZeroLifeChecker : ISBAChecker
    {
        public void Check(StateBasedActions sba, GameCore gameCore)
        {
            if (gameCore.Player1.Life <= 0)
            {
                sba.AddAction(new SBAActionRecord
                {
                    Type = SBAActionType.ZeroLife,
                    AffectedEntity = gameCore.Player1
                });
            }
            if (gameCore.Player2.Life <= 0)
            {
                sba.AddAction(new SBAActionRecord
                {
                    Type = SBAActionType.ZeroLife,
                    AffectedEntity = gameCore.Player2
                });
            }
        }
    }

    /// <summary>
    /// 单位防御力归零检查器
    /// </summary>
    public class ZeroToughnessChecker : ISBAChecker
    {
        public void Check(StateBasedActions sba, GameCore gameCore)
        {
            foreach (var player in new[] { gameCore.Player1, gameCore.Player2 })
            {
                var battlefield = gameCore.ZoneManager.GetCards(player, Zone.Battlefield);
                foreach (var card in battlefield)
                {
                    if (card is IHasLife hasLife && hasLife.Life <= 0 && card.IsAlive)
                    {
                        sba.AddAction(new SBAActionRecord
                        {
                            Type = SBAActionType.ZeroToughness,
                            AffectedEntity = card
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// 区域变更检查器
    /// </summary>
    public class ZoneChangeChecker : ISBAChecker
    {
        public void Check(StateBasedActions sba, GameCore gameCore)
        {
            // 检查是否有卡牌需要区域变更触发
            // 当前为占位实现，实际需要追踪区域变更
        }
    }

    /// <summary>
    /// 文本变更检查器
    /// </summary>
    public class TextChangeChecker : ISBAChecker
    {
        public void Check(StateBasedActions sba, GameCore gameCore)
        {
            // 检查文本修改触发
            // 当前为占位实现
        }
    }

    /// <summary>
    /// 特征变更检查器
    /// </summary>
    public class CharacteristicChangeChecker : ISBAChecker
    {
        public void Check(StateBasedActions sba, GameCore gameCore)
        {
            // 检查特征变化触发
            // 当前为占位实现
        }
    }
}
