using CardCore.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    // ================================================================
    // 筛选器接口
    // ================================================================

    /// <summary>
    /// 目标筛选器接口
    /// 定义如何从候选列表中筛选合法目标
    /// </summary>
    public interface ITargetFilter
    {
        /// <summary>筛选器显示名称</summary>
        string DisplayName { get; }

        /// <summary>
        /// 从候选列表中筛选合法目标
        /// </summary>
        List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context);
    }

    // ================================================================
    // 内置筛选器实现
    // ================================================================

    /// <summary>己方筛选器</summary>
    public class FriendlyFilter : ITargetFilter
    {
        public string DisplayName => "己方";
        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e =>
            {
                if (e is Card card) return card.GetController() == context.Controller;
                return false;
            }).ToList();
        }
    }

    /// <summary>敌方筛选器</summary>
    public class EnemyFilter : ITargetFilter
    {
        public string DisplayName => "敌方";
        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e =>
            {
                if (e is Card card) return card.GetController() != context.Controller;
                return false;
            }).ToList();
        }
    }

    /// <summary>卡牌类型筛选器</summary>
    public class CardTypeFilter : ITargetFilter
    {
        private readonly Cardtype _cardType;
        public string DisplayName => $"类型:{_cardType}";

        public CardTypeFilter(Cardtype cardType) { _cardType = cardType; }

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e =>
            {
                if (e is IHasSupertype hasType) return hasType.Supertype == _cardType;
                return false;
            }).ToList();
        }
    }

    /// <summary>属性比较筛选器（攻击力/生命值大于/小于/等于阈值）</summary>
    public class StatComparisonFilter : ITargetFilter
    {
        public enum ComparisonOp { GreaterThan, LessThan, Equal, GreaterOrEqual, LessOrEqual }

        private readonly string _statName; // "Power" or "Life"
        private readonly ComparisonOp _op;
        private readonly int _threshold;

        public string DisplayName => $"{_statName} {_OpText()} {_threshold}";

        public StatComparisonFilter(string statName, ComparisonOp op, int threshold)
        {
            _statName = statName;
            _op = op;
            _threshold = threshold;
        }

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => Compare(GetStatValue(e))).ToList();
        }

        private int GetStatValue(Entity e)
        {
            return _statName == "Power" ? e.GetPower() : e.GetLife();
        }

        private bool Compare(int value)
        {
            return _op switch
            {
                ComparisonOp.GreaterThan => value > _threshold,
                ComparisonOp.LessThan => value < _threshold,
                ComparisonOp.Equal => value == _threshold,
                ComparisonOp.GreaterOrEqual => value >= _threshold,
                ComparisonOp.LessOrEqual => value <= _threshold,
                _ => false
            };
        }

        private string _OpText()
        {
            return _op switch
            {
                ComparisonOp.GreaterThan => ">",
                ComparisonOp.LessThan => "<",
                ComparisonOp.Equal => "=",
                ComparisonOp.GreaterOrEqual => ">=",
                ComparisonOp.LessOrEqual => "<=",
                _ => "?"
            };
        }
    }

    /// <summary>关键词筛选器</summary>
    public class KeywordFilter : ITargetFilter
    {
        private readonly string _keyword;
        private readonly bool _mustHave;
        public string DisplayName => _mustHave ? $"拥有:{_keyword}" : $"没有:{_keyword}";

        /// <param name="keyword">关键词名称</param>
        /// <param name="mustHave">true=必须拥有，false=必须没有</param>
        public KeywordFilter(string keyword, bool mustHave = true)
        {
            _keyword = keyword;
            _mustHave = mustHave;
        }

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => e.HasKeyword(_keyword) == _mustHave).ToList();
        }
    }

    /// <summary>区域筛选器</summary>
    public class ZoneFilter : ITargetFilter
    {
        private readonly Zone _zone;
        public string DisplayName => $"区域:{_zone}";

        public ZoneFilter(Zone zone) { _zone = zone; }

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => e.GetZone() == _zone).ToList();
        }
    }

    /// <summary>未横置筛选器</summary>
    public class UntappedFilter : ITargetFilter
    {
        public string DisplayName => "未横置";
        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => !e.IsTapped()).ToList();
        }
    }

    /// <summary>已横置筛选器</summary>
    public class TappedFilter : ITargetFilter
    {
        public string DisplayName => "已横置";
        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => e.IsTapped()).ToList();
        }
    }

    /// <summary>生物类型筛选器</summary>
    public class SubtypeFilter : ITargetFilter
    {
        private readonly CardSubtype _subtype;
        public string DisplayName => $"种族:{_subtype}";

        public SubtypeFilter(CardSubtype subtype) { _subtype = subtype; }

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e =>
            {
                if (e is IHasSubtypes hasSubtypes) return hasSubtypes.Subtypes.HasFlag(_subtype);
                return false;
            }).ToList();
        }
    }

    /// <summary>受伤筛选器</summary>
    public class DamagedFilter : ITargetFilter
    {
        public string DisplayName => "已受伤";
        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => e.GetLife() < e.GetMaxLife()).ToList();
        }
    }

    // ================================================================
    // 组合筛选器
    // ================================================================

    /// <summary>
    /// 组合筛选器（AND逻辑）
    /// 所有子筛选器都必须通过
    /// </summary>
    public class CompositeFilter : ITargetFilter
    {
        private readonly List<ITargetFilter> _filters;
        public string DisplayName => string.Join(" + ", _filters.Select(f => f.DisplayName));

        public CompositeFilter(params ITargetFilter[] filters)
        {
            _filters = new List<ITargetFilter>(filters);
        }

        public CompositeFilter(IEnumerable<ITargetFilter> filters)
        {
            _filters = new List<ITargetFilter>(filters);
        }

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            var result = candidates;
            foreach (var filter in _filters)
            {
                result = filter.Filter(result, context);
                if (result.Count == 0) break;
            }
            return result;
        }

        public void AddFilter(ITargetFilter filter) => _filters.Add(filter);
    }

    // ================================================================
    // 目标解析器
    // ================================================================

    /// <summary>
    /// 目标解析器
    /// 负责从 AtomicEffectConfig 的配置生成合法候选目标列表
    /// </summary>
    public class TargetResolver
    {
        private readonly ZoneManager _zoneManager;

        public TargetResolver(ZoneManager zoneManager)
        {
            _zoneManager = zoneManager;
        }

        /// <summary>
        /// 获取候选目标列表
        /// 根据 AtomicEffectConfig 的 TargetType 确定初始候选池
        /// </summary>
        public List<Entity> GetCandidates(AtomicEffectConfig config, EffectExecutionContext context)
        {
            var candidates = new List<Entity>();

            if (context.Controller == null || _zoneManager == null)
                return candidates;

            var opponent = context.Controller.Opponent;

            switch (config.TargetType)
            {
                case EffectTargetType.Self:
                    candidates.Add(context.Source);
                    break;

                case EffectTargetType.Target:
                    // 需要玩家选择，先返回所有可能的目标
                    candidates.AddRange(GetAllBattlefieldCreatures(context.Controller, opponent));
                    candidates.Add(opponent);
                    candidates.Add(context.Controller);
                    break;

                case EffectTargetType.AllEnemies:
                    candidates.AddRange(GetBattlefieldCreatures(opponent));
                    candidates.Add(opponent);
                    break;

                case EffectTargetType.AllAllies:
                    candidates.AddRange(GetBattlefieldCreatures(context.Controller));
                    candidates.Add(context.Controller);
                    break;

                case EffectTargetType.All:
                    candidates.AddRange(GetAllBattlefieldCreatures(context.Controller, opponent));
                    candidates.Add(opponent);
                    candidates.Add(context.Controller);
                    break;

                case EffectTargetType.Random:
                    candidates.AddRange(GetAllBattlefieldCreatures(context.Controller, opponent));
                    candidates.Add(opponent);
                    candidates.Add(context.Controller);
                    break;

                case EffectTargetType.Opponent:
                    candidates.Add(opponent);
                    break;

                case EffectTargetType.Owner:
                case EffectTargetType.Controller:
                    candidates.Add(context.Controller);
                    break;

                case EffectTargetType.None:
                    break;
            }

            return candidates;
        }

        /// <summary>
        /// 应用筛选器链
        /// </summary>
        public List<Entity> ApplyFilters(List<Entity> candidates, List<ITargetFilter> filters, EffectExecutionContext context)
        {
            if (filters == null || filters.Count == 0)
                return candidates;

            var composite = new CompositeFilter(filters);
            return composite.Filter(candidates, context);
        }

        /// <summary>
        /// 解析 AtomicEffectConfig 中的 TargetFilter 字符串为筛选器列表
        /// </summary>
        public List<ITargetFilter> ParseFilters(string filterString)
        {
            var filters = new List<ITargetFilter>();
            if (string.IsNullOrEmpty(filterString)) return filters;

            foreach (var token in filterString.Split(','))
            {
                var trimmed = token.Trim();
                switch (trimmed)
                {
                    case "Creature":
                        filters.Add(new CardTypeFilter(Cardtype.Creature));
                        break;
                    case "Player":
                        // Player本身不是筛选条件，候选池已包含Player
                        break;
                    case "Untapped":
                        filters.Add(new UntappedFilter());
                        break;
                    case "Tapped":
                        filters.Add(new TappedFilter());
                        break;
                    case "Damaged":
                        filters.Add(new DamagedFilter());
                        break;
                    case "Friendly":
                        filters.Add(new FriendlyFilter());
                        break;
                    case "Enemy":
                        filters.Add(new EnemyFilter());
                        break;
                    // 支持格式: Power>5, Life<3 等
                    default:
                        var parsed = TryParseStatComparison(trimmed);
                        if (parsed != null) filters.Add(parsed);
                        break;
                }
            }

            return filters;
        }

        private ITargetFilter TryParseStatComparison(string token)
        {
            // 格式: "Power>5" or "Life<=3"
            foreach (StatComparisonFilter.ComparisonOp op in Enum.GetValues(typeof(StatComparisonFilter.ComparisonOp)))
            {
                string opStr = op switch
                {
                    StatComparisonFilter.ComparisonOp.GreaterThan => ">",
                    StatComparisonFilter.ComparisonOp.LessThan => "<",
                    StatComparisonFilter.ComparisonOp.Equal => "=",
                    StatComparisonFilter.ComparisonOp.GreaterOrEqual => ">=",
                    StatComparisonFilter.ComparisonOp.LessOrEqual => "<=",
                    _ => ""
                };

                int idx = token.IndexOf(opStr);
                if (idx > 0)
                {
                    string statName = token.Substring(0, idx);
                    if ((statName == "Power" || statName == "Life") &&
                        int.TryParse(token.Substring(idx + opStr.Length), out int threshold))
                    {
                        return new StatComparisonFilter(statName, op, threshold);
                    }
                }
            }
            return null;
        }

        private List<Entity> GetAllBattlefieldCreatures(Player p1, Player p2)
        {
            var result = new List<Entity>();
            result.AddRange(GetBattlefieldCreatures(p1));
            result.AddRange(GetBattlefieldCreatures(p2));
            return result;
        }

        private List<Entity> GetBattlefieldCreatures(Player player)
        {
            if (player == null || _zoneManager == null) return new List<Entity>();
            var container = _zoneManager.GetZoneContainer(player);
            if (container == null) return new List<Entity>();
            return container.GetCards(Zone.Battlefield).Cast<Entity>().ToList();
        }
    }
}
