# Synergy - TCG/CCG 卡牌游戏规则引擎

## 项目简介
双人卡牌对战游戏（TCG/CCG）的规则引擎框架，采用类似《游戏王》和《万智牌》（MTG）的连锁系统和层系统设计。

## 代码风格
- 使用 PascalCase 命名类和方法
- 使用 camelCase 命名私有变量
- 所有 TODO 标记需要在完成时移除或更新

## 核心架构
```
GameCore (顶层调度器)
├── TurnEngine        - 回合阶段状态机
├── StackEngine       - 连锁/优先权引擎
│   └── EffectExecutor - 效果执行器
├── TriggerEngine     - 触发式效果引擎
├── LayerEngine       - 层系统（持续效果计算）
├── StateBasedActions - 状态动作检查系统
├── ReplacementEngine - 替代效果引擎
├── ZoneManager       - 区域管理器
├── ElementPoolSystem - 元素池系统（横置产费）
└── CombatSystem      - 战斗系统
```

## 目录结构
```
Assets/
├── Core/                          # 核心规则引擎
│   ├── GameCore.cs                # 顶层游戏核心
│   ├── Entity.cs                  # 实体基类 + Player
│   ├── Enums.cs                   # 枚举定义
│   │
│   ├── Cards/                     # 卡牌系统
│   │   ├── Card.cs                # 卡牌类
│   │   ├── CardExtensions.cs      # 卡牌扩展方法
│   │   └── CardTypes.cs           # 卡牌类型定义 + 接口
│   │
│   ├── Effects/                   # 效果系统
│   │   ├── Effect.cs              # 效果实例/基类
│   │   ├── EffectDefinition.cs    # 效果定义 + IStackObject
│   │   ├── EffectBuilder.cs       # 效果构建器
│   │   ├── EffectExecutionEngine.cs  # 效果执行引擎 + 栈引擎 + 触发引擎
│   │   ├── EffectResolutionContext.cs # 效果解析上下文
│   │   ├── AtomicEffects.cs       # 原子效果库（50+种）
│   │   ├── ActivationConditions.cs # 发动条件 + 条件检查器
│   │   ├── SpeedSystem.cs         # 速度系统
│   │   ├── TargetSystem.cs        # 目标选择系统
│   │   ├── CostSystem.cs          # 代价系统
│   │   ├── ReplacementEngine.cs   # 替代效果引擎
│   │   ├── ElementAffinity.cs     # 元素倾向系统
│   │   └── EffectTargeting.cs     # 效果目标系统
│   │
│   ├── Combat/                    # 战斗系统
│   │   └── CombatSystem.cs        # 完整战斗流程
│   │
│   ├── StateBasedActions/         # 状态动作系统
│   │   ├── StateBasedActions.cs   # SBA框架
│   │   └── SBACheckers.cs         # SBA检查器
│   │
│   ├── TurnSystem/                # 回合系统
│   │   └── TurnEngine.cs          # 回合引擎
│   │
│   ├── Layers/                    # 层系统
│   │   ├── LayerEngine.cs         # 层引擎
│   │   ├── ControlChangeLayer.cs  # 控制权变更层
│   │   ├── TextChangeLayer.cs     # 文本变更层
│   │   ├── CopyEffects.cs         # 复制效果
│   │   ├── CharacteristicDefiningAbilities.cs # 特征定义能力
│   │   └── ContinuousEffectDurationTracker.cs # 持续效果时长
│   │
│   ├── Events/                    # 事件系统
│   │   ├── GameEvents.cs          # 游戏事件定义（50+种）
│   │   ├── GameEvent.cs           # 事件基类
│   │   ├── GameEventBus.cs        # 事件总线
│   │   ├── EventManager.cs        # 事件管理器
│   │   └── IGameObserver.cs       # 观察者接口
│   │
│   ├── Zones/                     # 区域系统
│   │   ├── Zones.cs               # 区域管理器 + ZoneContainer
│   │   └── ElementPool.cs         # 元素池系统
│   │
│   ├── Systems/                   # 辅助系统
│   │   ├── AttributeSystem.cs     # 属性系统
│   │   └── TimestampSystem.cs     # 时间戳系统
│   │
│   ├── ValueSystem/               # 价值平衡系统
│   │   ├── ValueCalculator.cs     # 数值计算器
│   │   ├── ValueSystemConfig.cs   # 价值配置
│   │   └── ValueSystemExamples.cs # 使用示例
│   │
│   ├── Data/                      # 数据系统
│   │   ├── CardData.cs            # 卡牌数据
│   │   ├── CardDataRegistry.cs    # 卡牌注册表
│   │   ├── CardTemplateBuilder.cs # 卡牌模板构建器
│   │   ├── CardBuilderExtensions.cs # 卡牌构建扩展
│   │   └── EffectSystemExtensions.cs # 效果系统扩展
│   │
│   ├── Configs/                   # 配置文件
│   │   ├── EffectLogicLibrary.cs  # 效果逻辑库
│   │   ├── BaseEffectList.cs      # 基础效果列表
│   │   └── EffectListContainer.cs # 效果列表容器
│   │
│   ├── Rules/                     # 规则验证
│   │   └── CardRuleValidator.cs   # 卡牌规则验证器
│   │
│   └── Utility/                   # 工具类
│       ├── MonoSingleton.cs       # Unity单例基类
│       └── MurmurHash3.cs         # 哈希算法
│
├── UI/                            # UI系统
│   ├── UIManager.cs               # UI管理器
│   ├── BaseUI.cs                  # UI基类
│   ├── CardEditorUI.cs            # 卡牌编辑器UI
│   ├── CardListUI.cs              # 卡牌列表UI
│   ├── CardPreviewUI.cs           # 卡牌预览UI
│   ├── DeckLibraryUI.cs           # 牌库UI
│   ├── EffectEditorUI.cs          # 效果编辑器UI
│   └── ManaCostEditorUI.cs        # 法力费用编辑UI
│
├── Scenes/                        # 场景
│   └── MainMenuScene.unity        # 主菜单场景
│
├── Prefab/                        # 预制体
│
└── Plugins/                       # 插件
```

---

## 已实现功能

### ✅ 效果系统
- **AtomicEffects** - 50+种原子效果
  - 伤害与治疗：DealDamage, Heal, LifeLoss, DealCombatDamage
  - 卡牌移动：DrawCard, DiscardCard, MillCard, ReturnToHand, PutToBattlefield, Destroy, Exile, SearchDeck, ShuffleIntoDeck
  - 状态变更：Tap, Untap, ModifyPower, ModifyLife, SetPower, SetLife, AddKeyword, RemoveKeyword
  - 法力相关：AddMana, ConsumeMana
  - 控制相关：GainControl, PreventDamage, NegateEffect, CounterSpell
  - 特殊效果：CreateToken, CopyCard, TransformCard, SwapStats, Nullify
  - 红色效果：AoEDamage, SplitDamage, GrantHaste, GrantRush
  - 蓝色效果：DrawThenDiscard, ScryCards, FreezePermanent, RedirectTarget
  - 绿色效果：RampMana, SearchLand, UntapAll, FightTarget
  - 灰色效果：MoveToAnyZone, SearchAndReveal
  - 反规则效果：GrantCannotBeTargeted, GrantSpellShield, GrantImmunity
- **EffectDefinition** - 效果定义系统，支持目标选择、发动条件、费用
- **EffectBuilder** - 流式API构建效果
- **EffectExecutor** - 效果执行器，连接定义与原子效果

### ✅ 战斗系统
**CombatSystem** - 完整战斗流程：
```
战斗阶段状态机：
None → SelectAttacker → DeclareAttack → SelectBlocker →
DeclareBlock → DamageCalculation → DamageDealing → EndCombat
```
- 攻击宣言（`CanDeclareAttack`, `DeclareAttack`）
- 阻挡宣言（`CanBlock`, `DeclareBlock`）
- 伤害计算（`CalculateDamage`）
- 伤害结算（`ExecuteDamage`）
- 战斗伤害事件发布

### ✅ 状态动作系统 (SBA)
**StateBasedActions** - 状态动作检查框架：
- 检查器模式（ISBAChecker接口）
- **ZeroLifeChecker** - 玩家生命值归零
- **ZeroToughnessChecker** - 单位防御力归零
- **LegendaryRuleChecker** - 传奇规则检查
- 循环执行直到状态稳定（防死循环：MAX_STABILITY_CHECKS = 100）

### ✅ 连锁系统
**StackEngine** - 栈引擎：
- 速度计数器（SpeedCounter）
- 待发效果队列（PendingEffectQueue）
- 优先权传递
- LIFO栈结算

### ✅ 触发系统
**TriggerEngine** - 触发式效果引擎：
- 效果注册/注销
- 事件匹配
- 自动入栈

### ✅ 层系统
**LayerEngine** - 持续效果计算：
- 5层效果应用顺序（Layer1-Layer5）
- 控制权变更层
- 文本变更层
- 特征定义能力

### ✅ 区域系统
**ZoneManager** - 区域管理：
- ZoneContainer - 玩家区域容器
- 卡牌移动事件
- 区域检查
- 支持带位置参数的移动（DeckPosition）

### ✅ 事件系统
**GameEventBus** - 全局事件总线：
- 50+种游戏事件类型
- 回合事件：TurnStartEvent, TurnEndEvent, PhaseStartEvent, PhaseEndEvent
- 卡牌事件：CardDrawEvent, CardPlayEvent, CardMoveEvent, CardDestroyEvent, CardBanishEvent
- 战斗事件：AttackDeclarationEvent, BlockDeclarationEvent, CombatDamageEvent
- 效果事件：EffectActivateEvent, EffectResolveEvent, StackAddEvent, StackEmptyEvent

### ✅ UI系统
- **CardEditorUI** - 卡牌编辑器
- **CardListUI** - 卡牌列表
- **CardPreviewUI** - 卡牌预览
- **DeckLibraryUI** - 牌库管理
- **EffectEditorUI** - 效果编辑器
- **ManaCostEditorUI** - 法力费用编辑

## 待完成事项

### 🔴 高优先级

| 任务 | 说明 | 预估复杂度 |
|------|------|------------|
| 游戏流程测试 | 编写集成测试验证完整游戏流程 | 高 |
| 卡牌数据配置 | 创建实际卡牌数据 ScriptableObject | 中 |
| 战斗UI交互 | 战斗阶段的可视化交互 | 中 |
| 效果执行验证 | 验证各类原子效果的正确执行 | 高 |

### 🟡 中优先级

| 任务 | 说明 | 预估复杂度 |
|------|------|------------|
| 效果UI配置界面优化 | 效果构建器可视化配置增强 | 中 |
| AI系统 | AI决策、目标选择 | 高 |
| 网络同步 | 多人对战网络层 | 高 |
| 规则完善 | 更多边界情况处理 | 中 |
| 卡牌模板系统 | 支持更多卡牌类型模板 | 中 |

### 🟢 低优先级

| 任务 | 说明 | 预估复杂度 |
|------|------|------------|
| 测试覆盖 | 单元测试、集成测试 | 中 |
| 性能优化 | 效果查询缓存、事件系统优化 | 中 |
| 效果解析脚本 | 文本描述自动生成效果 | 高 |
| 回放系统 | 游戏过程录制与回放 | 中 |
| 教程系统 | 新手引导 | 低 |

### 📋 待修复/完善项

| 问题 | 文件 | 说明 |
|------|------|------|
| TODO: 实现展示逻辑 | AtomicEffects.cs:1108 | SearchDeckEffect 展示逻辑 |
| TODO: 实现洗牌和位置放置逻辑 | AtomicEffects.cs:1163 | ShuffleIntoDeckEffect |
| TODO: 实现自定义条件检查 | ActivationConditions.cs:792 | Custom条件类型 |
| TODO: 检查来源是否在场 | Effect.cs:369 | ContinuousEffect.IsValid |
| TODO: 检查触发条件 | EffectExecutionEngine.cs:673 | 触发条件检查 |

---

## 项目进度

- [2026-02-25] 卡牌数据系统和UI框架完成
- [2026-02-27] 效果构建系统完成
- [2026-02-27] 价值平衡系统完成
- [2026-03-01] 战斗系统完成
- [2026-03-02] 状态动作检查完成
- [2026-03-02] 效果执行逻辑完成

---

## 附录

### 游戏区域

| 区域 | 说明 |
|------|------|
| Hand | 手牌 |
| Deck | 牌库 |
| Battlefield | 战场 |
| Graveyard | 坟墓场 |
| Exile | 流放区 |
| ElementPool | 元素池（背面向上手牌作为法力源） |
| None | 无区域（衍生物临时卡牌） |

### 回合流程

```
Standby → Draw → Main → End

Standby: 横置恢复
Draw: 正常抽卡
Main: 发动效果/攻击宣言
End: 手牌上限检查
```

### 元素倾向系统
- ElementAffinity - 关联效果与元素颜色
- ElementPaymentValidator - 验证和执行元素支付
- ElementAffinities - 预定义的红/蓝/绿/灰倾向

### 效果免疫系统
- EffectTargetFlags - 目标状态标记（魔免、全抗等）
- IHasEffectImmunity - 效果免疫接口
- EffectTargetValidator - 目标验证器
- EffectImmunityGranter - 授予免疫

### 条件类型（ActivationConditions.cs）

| 类别 | 条件类型 |
|------|----------|
| 资源条件 | MinCardsInHand, MaxCardsInHand, MinCardsOnField, CardsInGraveyard, CardsInDeck, MinManaAvailable, SpecificManaTypeAvailable |
| 实体条件 | ControllerHasLife, OpponentHasLife, CardHasType, CardHasManaType, CardIsTapped, CardIsUntapped, CardHasPower, CardHasLife, HasKeyword, HasAbility |
| 时点条件 | OncePerTurn, OnlyMainPhase, OnlyOwnTurn, OnlyOpponentTurn, FirstTimeThisGame, FirstTimeThisTurn, DuringCombat, NotDuringCombat |
| 场地条件 | FieldHasCardType, OpponentFieldHasCardType, HandHasCardType, GraveyardHasCardType |
| 伤害条件 | DamageDealtThisTurn, DamageTakenThisTurn, CombatDamageDealt, CombatDamageTaken |
| 战斗条件 | Attacking, Blocking, BlockedThisTurn, WasBlocked |
| 连锁条件 | StackHasEffects, StackEmpty, HasPriority |
| 复合条件 | And, Or, Not, Custom |
