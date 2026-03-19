using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using CardCore.Data;

namespace CardCore
{
    #region 枚举定义

    /// <summary>
    /// 目标分类（一级分类）
    /// </summary>
    public enum TargetCategory
    {
        卡牌,
        战场单位,
        玩家,
        特殊目标
    }

    #endregion

    #region 比较条件

    /// <summary>
    /// 比较条件（用于数值比较）
    /// </summary>
    [Serializable]
    public class ComparisonCondition
    {
        /// <summary>比较运算符</summary>
        public ComparisonOperator Operator;

        /// <summary>比较值</summary>
        public int Value;

        /// <summary>
        /// 检查是否满足条件
        /// </summary>
        public bool Evaluate(int actualValue)
        {
            return Operator switch
            {
                ComparisonOperator.等于 => actualValue == Value,
                ComparisonOperator.不等于 => actualValue != Value,
                ComparisonOperator.大于 => actualValue > Value,
                ComparisonOperator.大于等于 => actualValue >= Value,
                ComparisonOperator.小于 => actualValue < Value,
                ComparisonOperator.小于等于 => actualValue <= Value,
                _ => false
            };
        }

        /// <summary>
        /// 创建等于条件
        /// </summary>
        public static ComparisonCondition Equal(int value) => new() { Operator = ComparisonOperator.等于, Value = value };

        /// <summary>
        /// 创建大于等于条件
        /// </summary>
        public static ComparisonCondition AtLeast(int value) => new() { Operator = ComparisonOperator.大于等于, Value = value };

        /// <summary>
        /// 创建小于等于条件
        /// </summary>
        public static ComparisonCondition AtMost(int value) => new() { Operator = ComparisonOperator.小于等于, Value = value };

        /// <summary>
        /// 创建大于条件
        /// </summary>
        public static ComparisonCondition GreaterThan(int value) => new() { Operator = ComparisonOperator.大于, Value = value };

        /// <summary>
        /// 创建小于条件
        /// </summary>
        public static ComparisonCondition LessThan(int value) => new() { Operator = ComparisonOperator.小于, Value = value };
    }

    #endregion

    #region 目标筛选器

    /// <summary>
    /// 目标筛选条件
    /// </summary>
    [Serializable]
    public class TargetFilter
    {
        /// <summary>卡牌类型筛选（null = 不限制）</summary>
        public CardType? CardType;

        /// <summary>法力颜色筛选（null = 不限制）</summary>
        public ManaType? ManaType;

        /// <summary>攻击力条件</summary>
        public ComparisonCondition PowerCondition;

        /// <summary>生命值条件</summary>
        public ComparisonCondition LifeCondition;

        /// <summary>区域筛选</summary>
        public Zone? TargetZone;

        /// <summary>横置状态筛选（null = 不限制）</summary>
        public bool? IsTapped;

        /// <summary>名称包含</summary>
        public string NameContains;

        /// <summary>控制者筛选（null = 不限制）</summary>
        public Player Owner;

        /// <summary>排除自身</summary>
        public bool ExcludeSelf = false;

        /// <summary>排除来源</summary>
        public bool ExcludeSource = true;

        /// <summary>自定义条件ID（用于复杂条件）</summary>
        public string CustomConditionId;

        /// <summary>
        /// 检查卡牌是否满足筛选条件
        /// </summary>
        public bool Matches(Card card, Entity source = null, Player controller = null)
        {
            // 排除自身
            if (ExcludeSource && source != null && card == source)
                return false;

            // 排除来源卡
            if (ExcludeSelf && card == source)
                return false;

            // 卡牌类型筛选
            if (CardType.HasValue && card is IHasCardType hasCardType)
            {
                if (hasCardType.CardType != CardType.Value)
                    return false;
            }

            // 法力颜色筛选
            if (ManaType.HasValue && card is IHasCost hasCost)
            {
                if (!hasCost.Cost.ContainsKey((int)ManaType.Value))
                    return false;
            }

            // 攻击力条件
            if (PowerCondition != null && card is IHasPower hasPower)
            {
                if (!PowerCondition.Evaluate(hasPower.Power))
                    return false;
            }

            // 生命值条件
            if (LifeCondition != null && card is IHasLife hasLife)
            {
                if (!LifeCondition.Evaluate(hasLife.Life))
                    return false;
            }

            // 名称包含
            if (!string.IsNullOrEmpty(NameContains) && card is IHasName hasName)
            {
                if (!hasName.CardName.Contains(NameContains))
                    return false;
            }

            // 控制者筛选
            if (Owner != null && controller != null)
            {
                if (Owner != controller)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 检查玩家是否满足筛选条件
        /// </summary>
        public bool MatchesPlayer(Player player)
        {
            // 玩家目标通常只需要检查是否为对手或自己
            return true;
        }

        /// <summary>
        /// 创建默认筛选器
        /// </summary>
        public static TargetFilter Default => new TargetFilter();

        /// <summary>
        /// 创建场上单位筛选器
        /// </summary>
        public static TargetFilter BattlefieldUnit(CardType? cardType = null)
        {
            return new TargetFilter
            {
                TargetZone = Zone.战场,
                CardType = cardType ?? CardType.生物
            };
        }

        /// <summary>
        /// 创建手牌筛选器
        /// </summary>
        public static TargetFilter HandCard(CardType? cardType = null)
        {
            return new TargetFilter
            {
                TargetZone = Zone.战场, // Note: Hand zone not in Luban Zone enum
                CardType = cardType
            };
        }

        /// <summary>
        /// 创建墓地筛选器
        /// </summary>
        public static TargetFilter GraveyardCard(CardType? cardType = null)
        {
            return new TargetFilter
            {
                TargetZone = Zone.坟墓场,
                CardType = cardType
            };
        }
    }

    #endregion

    #region 目标选择器

    /// <summary>
    /// 目标选择器
    /// 定义效果如何选择目标
    /// </summary>
    [Serializable]
    public class TargetSelector
    {
        /// <summary>主目标类型</summary>
        public TargetType PrimaryTarget;

        /// <summary>目标分类（一级）</summary>
        public TargetCategory TargetCategory;

        /// <summary>子目标类型（二级）</summary>
        public SubTargetType SubTargetType;

        /// <summary>目标费用系数</summary>
        public float TargetCoefficient = 1.0f;

        /// <summary>目标筛选条件</summary>
        public TargetFilter Filter;

        /// <summary>最小目标数</summary>
        public int MinTargets = 1;

        /// <summary>最大目标数</summary>
        public int MaxTargets = 1;

        /// <summary>目标是否可选（可以为空）</summary>
        public bool Optional = false;

        /// <summary>是否需要玩家选择</summary>
        public bool RequiresPlayerSelection = true;

        /// <summary>
        /// 是否为单体目标
        /// </summary>
        public bool IsSingleTarget => MinTargets == 1 && MaxTargets == 1;

        /// <summary>
        /// 是否为多目标
        /// </summary>
        public bool IsMultiTarget => MaxTargets > 1;

        /// <summary>
        /// 是否为全体目标
        /// </summary>
        public bool IsAllTarget => MaxTargets == int.MaxValue;

        /// <summary>
        /// 获取描述
        /// </summary>
        public string GetDescription()
        {
            // 如果有子目标类型，使用新的显示方式
            if (SubTargetType != SubTargetType.自己 || TargetCategory != TargetCategory.玩家)
            {
                string subTargetName = TargetCoefficientConfig.GetDisplayName(SubTargetType);

                if (TargetCoefficientConfig.NeedsTargetCountInput(TargetCategory) && MaxTargets > 1)
                {
                    subTargetName = $"{subTargetName} × {MaxTargets}";
                }

                if (Optional)
                    subTargetName = $"（可选）{subTargetName}";

                return $"{subTargetName} (×{TargetCoefficient:F2})";
            }

            // 兼容旧的方式
            string targetDesc = PrimaryTarget switch
            {
                TargetType.自己 => "自己",
                TargetType.对手 => "对手",
                TargetType.任意玩家 => "任意玩家",
                TargetType.指定卡牌 => "指定卡牌",
                TargetType.所有卡牌 => "所有卡牌",
                TargetType.随机卡牌 => "随机卡牌",
                TargetType.这张卡 => "这张卡",
                TargetType.场上的卡 => "场上卡牌",
                TargetType.手牌中的卡 => "手牌",
                TargetType.墓地中的卡 => "墓地卡牌",
                TargetType.牌库中的卡 => "牌库卡牌",
                TargetType.触发事件目标 => "触发目标",
                TargetType.触发事件来源 => "触发来源",
                _ => "目标"
            };

            if (MaxTargets > 1)
                targetDesc = $"至多{MaxTargets}个{targetDesc}";

            if (Optional)
                targetDesc = $"（可选）{targetDesc}";

            return targetDesc;
        }

        #region 静态工厂方法

        /// <summary>选择自己</summary>
        public static TargetSelector Self() => new()
        {
            PrimaryTarget = TargetType.自己,
            MinTargets = 1,
            MaxTargets = 1,
            RequiresPlayerSelection = false
        };

        /// <summary>选择对手</summary>
        public static TargetSelector Opponent() => new()
        {
            PrimaryTarget = TargetType.对手,
            MinTargets = 1,
            MaxTargets = 1,
            RequiresPlayerSelection = false
        };

        /// <summary>选择任意玩家</summary>
        public static TargetSelector AnyPlayer() => new()
        {
            PrimaryTarget = TargetType.任意玩家,
            MinTargets = 1,
            MaxTargets = 1
        };

        /// <summary>选择指定卡牌</summary>
        public static TargetSelector TargetCard(TargetFilter filter = null, int min = 1, int max = 1) => new()
        {
            PrimaryTarget = TargetType.指定卡牌,
            Filter = filter,
            MinTargets = min,
            MaxTargets = max,
            RequiresPlayerSelection = true
        };

        /// <summary>选择场上单位</summary>
        public static TargetSelector BattlefieldUnit(TargetFilter filter = null) => new()
        {
            PrimaryTarget = TargetType.场上的卡,
            Filter = filter ?? TargetFilter.BattlefieldUnit(),
            MinTargets = 1,
            MaxTargets = 1
        };

        /// <summary>选择所有场上单位</summary>
        public static TargetSelector AllBattlefieldUnits(TargetFilter filter = null) => new()
        {
            PrimaryTarget = TargetType.所有卡牌,
            Filter = filter ?? TargetFilter.BattlefieldUnit(),
            MinTargets = 0,
            MaxTargets = int.MaxValue,
            RequiresPlayerSelection = false
        };

        /// <summary>选择这张卡</summary>
        public static TargetSelector ThisCard() => new()
        {
            PrimaryTarget = TargetType.这张卡,
            MinTargets = 1,
            MaxTargets = 1,
            RequiresPlayerSelection = false
        };

        /// <summary>选择随机卡牌</summary>
        public static TargetSelector RandomCard(TargetFilter filter = null, int count = 1) => new()
        {
            PrimaryTarget = TargetType.随机卡牌,
            Filter = filter,
            MinTargets = count,
            MaxTargets = count,
            RequiresPlayerSelection = false
        };

        /// <summary>选择手牌</summary>
        public static TargetSelector CardsInHand(TargetFilter filter = null, int min = 1, int max = 1) => new()
        {
            PrimaryTarget = TargetType.手牌中的卡,
            Filter = filter ?? TargetFilter.HandCard(),
            MinTargets = min,
            MaxTargets = max
        };

        /// <summary>选择墓地卡牌</summary>
        public static TargetSelector CardsInGraveyard(TargetFilter filter = null, int min = 1, int max = 1) => new()
        {
            PrimaryTarget = TargetType.墓地中的卡,
            Filter = filter ?? TargetFilter.GraveyardCard(),
            MinTargets = min,
            MaxTargets = max
        };

        #endregion
    }

    #endregion

    #region 目标解析器

    /// <summary>
    /// 目标解析器
    /// 根据目标选择器解析实际目标
    /// </summary>
    public class TargetResolver
    {
        private ZoneManager _zoneManager;

        public TargetResolver(ZoneManager zoneManager)
        {
            _zoneManager = zoneManager;
        }

        /// <summary>
        /// 解析目标
        /// </summary>
        /// <param name="selector">目标选择器</param>
        /// <param name="source">来源实体（发动效果的卡牌）</param>
        /// <param name="controller">控制者</param>
        /// <param name="triggeringEvent">触发事件（如果有）</param>
        /// <returns>目标列表</returns>
        public List<Entity> ResolveTargets(
            TargetSelector selector,
            Entity source,
            Player controller,
            IGameEvent triggeringEvent = null)
        {
            var targets = new List<Entity>();

            switch (selector.PrimaryTarget)
            {
                case TargetType.自己:
                    targets.Add(controller);
                    break;

                case TargetType.对手:
                    targets.Add(controller.Opponent);
                    break;

                case TargetType.任意玩家:
                    // 需要玩家选择，这里返回空让上层处理
                    break;

                case TargetType.这张卡:
                    if (source != null)
                        targets.Add(source);
                    break;

                case TargetType.触发事件目标:
                    if (triggeringEvent != null)
                    {
                        var target = ExtractTargetFromEvent(triggeringEvent);
                        if (target != null)
                            targets.Add(target);
                    }
                    break;

                case TargetType.触发事件来源:
                    if (triggeringEvent != null)
                    {
                        var eventSource = ExtractSourceFromEvent(triggeringEvent);
                        if (eventSource != null)
                            targets.Add(eventSource);
                    }
                    break;

                case TargetType.指定卡牌:
                case TargetType.场上的卡:
                case TargetType.手牌中的卡:
                case TargetType.墓地中的卡:
                case TargetType.牌库中的卡:
                case TargetType.除外区的卡:
                    // 需要玩家选择或自动筛选
                    targets.AddRange(GetCardsFromZone(selector, controller));
                    break;

                case TargetType.所有卡牌:
                    targets.AddRange(GetAllMatchingCards(selector, controller));
                    break;

                case TargetType.随机卡牌:
                    targets.AddRange(GetRandomCards(selector, controller));
                    break;
            }

            return targets;
        }

        /// <summary>
        /// 获取可选目标列表（用于玩家选择）
        /// </summary>
        public List<Entity> GetValidTargets(
            TargetSelector selector,
            Entity source,
            Player controller)
        {
            var validTargets = new List<Entity>();

            // 玩家目标
            if (selector.PrimaryTarget == TargetType.任意玩家)
            {
                if (selector.Filter?.MatchesPlayer(controller) ?? true)
                    validTargets.Add(controller);
                if (selector.Filter?.MatchesPlayer(controller.Opponent) ?? true)
                    validTargets.Add(controller.Opponent);
                return validTargets;
            }

            // 卡牌目标
            if (IsCardTarget(selector.PrimaryTarget))
            {
                var cards = GetCardsFromZone(selector, controller);
                foreach (var card in cards)
                {
                    if (selector.Filter?.Matches(card as Card, source, controller) ?? true)
                        validTargets.Add(card);
                }
            }

            return validTargets;
        }

        /// <summary>
        /// 检查是否有有效目标
        /// </summary>
        public bool HasValidTargets(TargetSelector selector, Entity source, Player controller)
        {
            return GetValidTargets(selector, source, controller).Count >= selector.MinTargets;
        }

        #region 私有方法

        private bool IsCardTarget(TargetType type)
        {
            return type switch
            {
                TargetType.指定卡牌 or
                TargetType.所有卡牌 or
                TargetType.随机卡牌 or
                TargetType.这张卡 or
                TargetType.场上的卡 or
                TargetType.手牌中的卡 or
                TargetType.墓地中的卡 or
                TargetType.牌库中的卡 or
                TargetType.除外区的卡 => true,
                _ => false
            };
        }

        private Zone GetZoneFromTargetType(TargetType type)
        {
            return type switch
            {
                TargetType.场上的卡 => Zone.战场,
                TargetType.手牌中的卡 => Zone.战场, // Hand not in Luban enum
                TargetType.墓地中的卡 => Zone.坟墓场,
                TargetType.牌库中的卡 => Zone.牌库,
                TargetType.除外区的卡 => Zone.流放区,
                _ => Zone.战场
            };
        }

        private List<Card> GetCardsFromZone(TargetSelector selector, Player controller)
        {
            var container = _zoneManager.GetZoneContainer(controller);
            Zone zone = GetZoneFromTargetType(selector.PrimaryTarget);

            var cards = container.GetCards(zone);

            // 应用筛选器
            if (selector.Filter != null)
            {
                cards = cards.Where(c => selector.Filter.Matches(c)).ToList();
            }

            return cards;
        }

        private List<Card> GetAllMatchingCards(TargetSelector selector, Player controller)
        {
            var allCards = new List<Card>();

            // 获取双方场上卡牌
            var selfContainer = _zoneManager.GetZoneContainer(controller);
            var opponentContainer = _zoneManager.GetZoneContainer(controller.Opponent);

            allCards.AddRange(selfContainer.GetCards(Zone.战场));
            allCards.AddRange(opponentContainer.GetCards(Zone.战场));

            // 应用筛选器
            if (selector.Filter != null)
            {
                allCards = allCards.Where(c => selector.Filter.Matches(c)).ToList();
            }

            return allCards;
        }

        private List<Card> GetRandomCards(TargetSelector selector, Player controller)
        {
            var allCards = GetCardsFromZone(selector, controller);
            var random = new Random();

            return allCards
                .OrderBy(x => random.Next())
                .Take(selector.MaxTargets)
                .ToList();
        }

        private Entity ExtractTargetFromEvent(IGameEvent gameEvent)
        {
            return gameEvent switch
            {
                DamageEvent damageEvent => damageEvent.Target,
                AttackDeclarationEvent attackEvent => attackEvent.Target,
                CardMoveEvent moveEvent => moveEvent.MovedCard,
                CardDestroyEvent destroyEvent => destroyEvent.DestroyedCard,
                _ => null
            };
        }

        private Entity ExtractSourceFromEvent(IGameEvent gameEvent)
        {
            return gameEvent switch
            {
                DamageEvent damageEvent => damageEvent.Source,
                AttackDeclarationEvent attackEvent => attackEvent.Attacker,
                CardDestroyEvent destroyEvent => destroyEvent.Source,
                _ => null
            };
        }

        #endregion
    }

    #endregion

    #region 目标上下文

    /// <summary>
    /// 目标上下文
    /// 存储效果解析过程中的目���信息
    /// </summary>
    public class TargetContext
    {
        /// <summary>选中的目标列表</summary>
        public List<Entity> SelectedTargets { get; private set; } = new List<Entity>();

        /// <summary>主要目标</summary>
        public Entity PrimaryTarget => SelectedTargets.FirstOrDefault();

        /// <summary>所有玩家目标</summary>
        public IEnumerable<Player> PlayerTargets => SelectedTargets.OfType<Player>();

        /// <summary>所有卡牌目标</summary>
        public IEnumerable<Card> CardTargets => SelectedTargets.OfType<Card>();

        /// <summary>
        /// 添加目标
        /// </summary>
        public void AddTarget(Entity target)
        {
            SelectedTargets.Add(target);
        }

        /// <summary>
        /// 设置目标列表
        /// </summary>
        public void SetTargets(List<Entity> targets)
        {
            SelectedTargets = targets ?? new List<Entity>();
        }

        /// <summary>
        /// 清空目标
        /// </summary>
        public void Clear()
        {
            SelectedTargets.Clear();
        }

        /// <summary>
        /// 检查是否有目标
        /// </summary>
        public bool HasTargets => SelectedTargets.Count > 0;

        /// <summary>
        /// 获取第一个指定类型的目标
        /// </summary>
        public T GetFirstTarget<T>() where T : Entity
        {
            return SelectedTargets.OfType<T>().FirstOrDefault();
        }
    }

    #endregion

    #region 目标系数配置

    /// <summary>
    /// 目标系数配置
    /// 定义每个目标类型的费用系数
    /// </summary>
    public static class TargetCoefficientConfig
    {
        /// <summary>
        /// 子目标类型对应的费用系数
        /// </summary>
        private static readonly Dictionary<SubTargetType, float> Coefficients = new()
        {
            // 玩家
            { SubTargetType.自己, 1.0f },
            { SubTargetType.对手, 1.1f },
            { SubTargetType.任意玩家, 1.5f },
            { SubTargetType.双方玩家, 1.8f },

            // 卡牌
            { SubTargetType.己方卡组, 1.0f },
            { SubTargetType.对方卡组, 1.5f },
            { SubTargetType.己方手牌, 1.0f },
            { SubTargetType.对方手牌, 2.0f },
            { SubTargetType.己方墓地, 1.1f },
            { SubTargetType.对方墓地, 1.5f },
            { SubTargetType.己方除外区, 2.0f },
            { SubTargetType.对方除外区, 2.0f },

            // 战场单位
            { SubTargetType.己方生物, 1.0f },
            { SubTargetType.对方生物, 1.1f },
            { SubTargetType.己方领域, 1.0f },
            { SubTargetType.对方领域, 1.1f },

            // 特殊目标
            { SubTargetType.事件触发源, 1.0f },
            { SubTargetType.事件触发目标, 1.0f },
            { SubTargetType.进攻单位, 1.0f },
            { SubTargetType.防御单位, 1.0f }
        };

        /// <summary>
        /// 子目标类型的显示名称（带系数）
        /// </summary>
        private static readonly Dictionary<SubTargetType, string> DisplayNames = new()
        {
            // 玩家
            { SubTargetType.自己, "自己" },
            { SubTargetType.对手, "对手" },
            { SubTargetType.任意玩家, "任意玩家" },
            { SubTargetType.双方玩家, "双方玩家" },

            // 卡牌
            { SubTargetType.己方卡组, "己方卡组" },
            { SubTargetType.对方卡组, "对方卡组" },
            { SubTargetType.己方手牌, "己方手牌" },
            { SubTargetType.对方手牌, "对方手牌" },
            { SubTargetType.己方墓地, "己方墓地" },
            { SubTargetType.对方墓地, "对方墓地" },
            { SubTargetType.己方除外区, "己方除外区" },
            { SubTargetType.对方除外区, "对方除外区" },

            // 战场单位
            { SubTargetType.己方生物, "己方生物" },
            { SubTargetType.对方生物, "对方生物" },
            { SubTargetType.己方领域, "己方领域" },
            { SubTargetType.对方领域, "对方领域" },

            // 特殊目标
            { SubTargetType.事件触发源, "事件触发源" },
            { SubTargetType.事件触发目标, "事件触发所选目标" },
            { SubTargetType.进攻单位, "进攻单位" },
            { SubTargetType.防御单位, "防御单位" }
        };

        /// <summary>
        /// 目标分类对应的子目标类型列表
        /// </summary>
        public static readonly Dictionary<TargetCategory, SubTargetType[]> CategorySubTargets = new()
        {
            { TargetCategory.玩家, new[]
                {
                    SubTargetType.自己,
                    SubTargetType.对手,
                    SubTargetType.任意玩家,
                    SubTargetType.双方玩家
                }
            },
            { TargetCategory.卡牌, new[]
                {
                    SubTargetType.己方卡组,
                    SubTargetType.对方卡组,
                    SubTargetType.己方手牌,
                    SubTargetType.对方手牌,
                    SubTargetType.己方墓地,
                    SubTargetType.对方墓地,
                    SubTargetType.己方除外区,
                    SubTargetType.对方除外区
                }
            },
            { TargetCategory.战场单位, new[]
                {
                    SubTargetType.己方生物,
                    SubTargetType.对方生物,
                    SubTargetType.己方领域,
                    SubTargetType.对方领域
                }
            },
            { TargetCategory.特殊目标, new[]
                {
                    SubTargetType.事件触发源,
                    SubTargetType.事件触发目标,
                    SubTargetType.进攻单位,
                    SubTargetType.防御单位
                }
            }
        };

        /// <summary>
        /// 获取子目标的费用系数
        /// </summary>
        public static float GetCoefficient(SubTargetType subTarget)
        {
            return Coefficients.TryGetValue(subTarget, out float coef) ? coef : 1.0f;
        }

        /// <summary>
        /// 获取子目标的显示名称
        /// </summary>
        public static string GetDisplayName(SubTargetType subTarget)
        {
            return DisplayNames.TryGetValue(subTarget, out string name) ? name : subTarget.ToString();
        }

        /// <summary>
        /// 获取子目标的显示名称（带系数）
        /// </summary>
        public static string GetDisplayNameWithCoefficient(SubTargetType subTarget)
        {
            string name = GetDisplayName(subTarget);
            float coef = GetCoefficient(subTarget);
            return $"{name} (×{coef:F1})";
        }

        /// <summary>
        /// 获取目标分类的显示名称
        /// </summary>
        public static string GetCategoryDisplayName(TargetCategory category)
        {
            return category switch
            {
                TargetCategory.玩家 => "玩家",
                TargetCategory.卡牌 => "卡牌",
                TargetCategory.战场单位 => "战场单位",
                TargetCategory.特殊目标 => "特殊目标",
                _ => category.ToString()
            };
        }

        /// <summary>
        /// 判断目标分类是否需要显示目标数输入框
        /// </summary>
        public static bool NeedsTargetCountInput(TargetCategory category)
        {
            return category == TargetCategory.卡牌 || category == TargetCategory.战场单位;
        }

        /// <summary>
        /// 计算最终系数（子目标系数 × 目标数）
        /// </summary>
        public static float CalculateFinalCoefficient(SubTargetType subTarget, int targetCount = 1)
        {
            float baseCoef = GetCoefficient(subTarget);
            return baseCoef * targetCount;
        }
    }

    #endregion
}