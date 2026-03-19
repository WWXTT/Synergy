## 原子效果配装流程与界面结构

---

## 一、整体架构

```
EffectBuilderUI (效果组装主界面)
├── 基本信息面板
├── 目标选择面板
├── 代价设置面板
├── 原子效果面板
├── 发动条件面板
└── 预览面板

AtomicEffectEditorPopup (原子效果编辑弹窗)
└── 配置具体效果参数

ConditionEditorPopup (条件编辑弹窗)
└── 配置发动条件
```

### 核心数据流

```
AtomicEffectData (可序列化数据)
        ↕ 转换
AtomicEffectInstance (运行时实例)
        ↕ 执行
AtomicEffectBase (具体效果实现)
```

---

## 二、效果组装主界面 (EffectBuilderUI)

### 1. 基本信息配置

| 字段 | 类型 | 说明 |
|------|------|------|
| DisplayName | string | 效果显示名称 |
| Description | string | 效果描述文本 |
| TriggerTiming | enum | 触发时点 |
| ActivationType | enum | 发动类型 |
| BaseSpeed | int | 基础速度 |

#### 触发时点 (TriggerTiming)

| 类型 | 说明 |
|------|------|
| Activate_Active | 主要阶段主动发动 |
| Activate_Instant | 瞬间发动（任意时点） |
| Activate_Response | 响应式发动 |
| On_EnterBattlefield | 入场时 |
| On_LeaveBattlefield | 离场时 |
| On_Death | 死亡时 |
| On_TurnStart | 回合开始 |
| On_TurnEnd | 回合结束 |
| On_AttackDeclare | 攻击宣言时 |
| On_CardDraw | 抽卡时 |
| On_CardPlay | 使用卡牌时 |

#### 发动类型 (ActivationType)

| 类型 | 说明 |
|------|------|
| Voluntary | 自由发动（玩家选择） |
| Automatic | 自动发动（条件满足自动触发） |
| Mandatory | 强制发动（必须执行） |

---

### 2. 目标选择配置

```
TargetSelectorData
├── PrimaryTarget   - 主目标类型
├── MinTargets      - 最小目标数
├── MaxTargets      - 最大目标数
├── Optional        - 目标是否可选
├── RequiresPlayerSelection - 是否需要玩家选择
└── Filter          - 目标筛选条件
    ├── CardType       - 卡牌类型筛选
    ├── ManaType       - 颜色筛选
    ├── PowerCondition - 攻击力条件
    ├── LifeCondition  - 生命值条件
    ├── TargetZone     - 区域筛选
    ├── IsTapped       - 横置状态筛选
    ├── NameContains   - 名称包含
    ├── ExcludeSelf    - 排除自身
    └── ExcludeSource  - 排除来源
```

#### 目标类型 (TargetType)

| 类型 | 说明 |
|------|------|
| Self | 自己 |
| Opponent | 对手 |
| AnyPlayer | 任意玩家 |
| Controller | 控制者 |
| TargetCard | 指定卡牌 |
| AllCards | 所有卡牌 |
| RandomCard | 随机卡牌 |
| ThisCard | 这张卡 |
| CardsInHand | 手牌 |
| CardsOnField | 场上卡牌 |
| CardsInGraveyard | 墓地卡牌 |
| CardsInDeck | 牌库卡牌 |
| CardsInExile | 除外区卡牌 |
| TriggerEventTarget | 触发目标 |
| AttackTarget | 攻击目标 |

---

### 3. 代价配置

```
ActivationCostData
├── ElementCosts    - 元素代价列表
│   └── ElementCostData
│       ├── ManaType - 元素类型
│       └── Amount   - 数量
│
├── ResourceCosts   - 资源代价列表
│   └── ResourceCostData
│       ├── FromZone       - 来源区域
│       ├── ToZone         - 去向区域
│       ├── Count          - 数量
│       └── RequireSelection - 是否需要选择
│
└── PrepayCosts     - 预支代价列表
    └── PrepayCostData
        ├── PrepayType  - 预支类型
        ├── Amount      - 预支数量
        └── RepayTurns  - 偿还回合数
```

#### 元素类型 (ManaType)

| 类型 | 说明 |
|------|------|
| Gray | 灰色（任意类型） |
| Red | 红色（伤害与破坏） |
| Blue | 蓝色（控制与知识） |
| Green | 绿色（成长与恢复） |
| White | 白色（保护与秩序） |
| Black | 黑色（死亡与牺牲） |

#### 资源区域

| 来源区域 (ResourceZone) | 去向区域 (DestinationZone) |
|------------------------|---------------------------|
| Hand - 手牌 | Graveyard - 墓地 |
| Battlefield - 场上 | Exile - 除外 |
| ExtraDeck - 额外卡组 | |
| Graveyard - 墓地 | |

#### 预支类型 (PrepayType)

| 类型 | 说明 |
|------|------|
| DrawCard | 预支抽卡（本回合多抽，下回合少抽） |
| ElementProduction | 预支元素产出 |
| AttackChance | 预支攻击次数 |
| NextTurnAction | 预支下回合行动 |

---

### 4. 原子效果面板

点击「添加原子效果」按钮，弹出 `AtomicEffectEditorPopup` 进行配置。

---

### 5. 发动条件面板

点击「添加条件」按钮，弹出 `ConditionEditorPopup` 进行配置。

---

## 三、原子效果编辑弹窗 (AtomicEffectEditorPopup)

### 参数配置界面

| 参数组 | 控件 | 显示条件 | 说明 |
|--------|------|----------|------|
| Value | InputField | 需要数值参数 | 主数值（伤害量/抽卡数等） |
| Value2 | InputField | 需要第二数值 | 次要参数 |
| StringValue | InputField | 需要字符串 | 关键词/衍生物ID |
| ManaType | Dropdown | 需要元素类型 | 指定元素颜色 |
| Zone | Dropdown | 需要区域 | 目标区域 |
| Duration | Dropdown | 需要持续时间 | 效果持续类型 |

### 效果分类（70+种）

#### 基础效果

| 类别 | 效果类型 | 中文名 |
|------|----------|--------|
| **伤害与治疗** | DealDamage | 造成伤害 |
| | DealCombatDamage | 造成战斗伤害 |
| | Heal | 回复生命 |
| | LifeLoss | 生命流失 |
| **卡牌移动** | DrawCard | 抽卡 |
| | DiscardCard | 弃牌 |
| | MillCard | 堆墓 |
| | ReturnToHand | 返回手牌 |
| | PutToBattlefield | 入场 |
| | Destroy | 销毁 |
| | Exile | 除外 |
| | ShuffleIntoDeck | 洗入牌库 |
| | SearchDeck | 检索牌库 |
| **状态变更** | Tap | 横置 |
| | Untap | 重置 |
| | ModifyPower | 修改攻击力 |
| | ModifyLife | 修改生命值 |
| | SetPower | 设置攻击力 |
| | SetLife | 设置生命值 |
| | AddCardType | 添加卡牌类型 |
| | RemoveCardType | 移除卡牌类型 |
| | AddKeyword | 添加关键词 |
| | RemoveKeyword | 移除关键词 |
| **法力相关** | AddMana | 添加法力 |
| | ConsumeMana | 消耗法力 |
| **控制相关** | GainControl | 获得控制权 |
| | PreventDamage | 防止伤害 |
| | NegateEffect | 无效效果 |
| | CounterSpell | 反制法术 |
| **特殊** | CreateToken | 创建衍生物 |
| | CopyCard | 复制卡牌 |
| | TransformCard | 转化卡牌 |
| | SwapStats | 交换属性 |
| | Nullify | 无效化卡牌 |

#### 红色效果（伤害与破坏）

| 效果类型 | 中文名 | 需要参数 |
|----------|--------|----------|
| AoEDamage | 范围伤害 | Value |
| SplitDamage | 分配伤害 | Value |
| TrampleDamage | 溢出伤害 | Value |
| DamageCannotBePrevented | 不可防止伤害 | Value |
| GrantHaste | 赋予敏捷 | Value |
| GrantRush | 赋予突袭 | Value |
| GrantDoubleStrike | 赋予双击 | Value |
| GrantMultiAttack | 赋予多攻 | Value, Value2 |
| DestroyArtifact | 破坏神器 | - |
| DestroyRandom | 随机破坏 | Value |

#### 蓝色效果（控制与知识）

| 效果类型 | 中文名 | 需要参数 |
|----------|--------|----------|
| CounterTargetSpell | 反制目标法术 | - |
| NegateActivation | 无效化发动 | - |
| RedirectTarget | 重定向目标 | - |
| DrawThenDiscard | 抽后弃 | Value, Value2 |
| ScryCards | 预见 | Value |
| FreezePermanent | 冻结永久物 | Value, Duration |
| StealControl | 偷取控制权 | - |
| SwapController | 交换控制者 | - |
| BounceToTop | 弹回牌库顶 | - |
| BounceToBottom | 弹回牌库底 | - |
| CopyExact | 精确复制 | - |

#### 绿色效果（成长与恢复）

| 效果类型 | 中文名 | 需要参数 |
|----------|--------|----------|
| RampMana | 法力加速 | Value, ManaType |
| SearchLand | 搜索地牌 | Value, ManaType |
| UntapAll | 全部重置 | Value |
| AddCounters | 添加指示物 | Value |
| DoubleCounters | 翻倍指示物 | Value |
| EvolveCreature | 进化生物 | Duration |
| FightTarget | 与目标战斗 | Value |
| GrantTrample | 赋予践踏 | Value |
| GrantReach | 赋予阻断飞行 | Value |
| RestoreToFullLife | 恢复满生命 | - |
| RemoveDebuffs | 移除减益 | StringValue |

#### 灰色效果（通用）

| 效果类型 | 中文名 | 需要参数 |
|----------|--------|----------|
| MoveToAnyZone | 移动到任意区域 | Zone |
| ExchangePosition | 交换位置 | Zone |
| SearchAndReveal | 检索并展示 | Value |
| SearchAndPlay | 检索并使用 | - |
| TransformInto | 转化为指定卡牌 | StringValue |
| MoveCard | 移动卡牌 | Zone |

#### 反规则效果

| 效果类型 | 中文名 | 需要参数 |
|----------|--------|----------|
| GrantCannotBeTargeted | 赋予不可被指定 | Duration |
| GrantSpellShield | 赋予法术护盾 | Value, Duration |
| GrantImmunity | 赋予免疫 | Duration |
| GrantUnaffected | 赋予不受影响 | Duration |
| ModifyGameRule | 修改游戏规则 | - |
| OverrideRestriction | 覆盖限制 | - |

### 持续时间类型 (DurationType)

| 类型 | 说明 |
|------|------|
| Once | 一次性效果 |
| UntilEndOfTurn | 直到回合结束 |
| UntilLeaveBattlefield | 直到离场 |
| WhileCondition | 条件满足时 |
| Permanent | 永久 |

---

## 四、条件编辑弹窗 (ConditionEditorPopup)

### 条件数据结构

```
ActivationConditionData
├── Type              - 条件类型
├── Value             - 数值参数
├── Value2            - 第二数值
├── CardTypeParam     - 卡牌类型参数
├── ManaTypeParam     - 元素类型参数
├── StringValue       - 字符串参数
├── Negate            - 是否取反
└── CustomConditionId - 自定义条件ID
```

### 条件类型分类

#### 资源条件

| 条件类型 | 中文名 | 参数 |
|----------|--------|------|
| MinCardsInHand | 手牌数量下限 | Value |
| MaxCardsInHand | 手牌数量上限 | Value |
| MinCardsOnField | 场上数量下限 | Value |
| MaxCardsOnField | 场上数量上限 | Value |
| CardsInGraveyard | 墓地数量条件 | Value |
| CardsInDeck | 牌库数量条件 | Value |
| MinManaAvailable | 可用元素下限 | Value |
| SpecificManaTypeAvailable | 特定元素可用 | Value, ManaTypeParam |

#### 实体条件

| 条件类型 | 中文名 | 参数 |
|----------|--------|------|
| ControllerHasLife | 控制者生命值 | Value |
| OpponentHasLife | 对手生命值 | Value |
| CardHasType | 卡牌类型条件 | CardTypeParam |
| CardHasManaType | 法力颜色条件 | ManaTypeParam |
| CardIsTapped | 卡牌已横置 | - |
| CardIsUntapped | 卡牌未横置 | - |
| CardHasPower | 攻击力条件 | Value |
| CardHasLife | 生命值条件 | Value |
| HasKeyword | 拥有关键词 | StringValue |
| HasAbility | 拥有异能 | StringValue |

#### 时点条件

| 条件类型 | 中文名 | 参数 |
|----------|--------|------|
| OncePerTurn | 每回合一次 | - |
| OnlyMainPhase | 仅主要阶段 | - |
| OnlyOwnTurn | 仅自己回合 | - |
| OnlyOpponentTurn | 仅对手回合 | - |
| FirstTimeThisGame | 本局首次 | - |
| FirstTimeThisTurn | 本回合首次 | - |
| DuringCombat | 战斗中 | - |
| NotDuringCombat | 非战斗中 | - |

#### 场地条件

| 条件类型 | 中文名 | 参数 |
|----------|--------|------|
| FieldHasCardType | 场上有特定类型 | Value, CardTypeParam |
| OpponentFieldHasCardType | 对手场上有特定类型 | Value, CardTypeParam |
| HandHasCardType | 手牌中有特定类型 | Value, CardTypeParam |
| GraveyardHasCardType | 墓地中有特定类型 | Value, CardTypeParam |

#### 伤害条件

| 条件类型 | 中文名 | 参数 |
|----------|--------|------|
| DamageDealtThisTurn | 本回合造成伤害 | Value |
| DamageTakenThisTurn | 本回合受到伤害 | Value |
| CombatDamageDealt | 造成战斗伤害 | - |
| CombatDamageTaken | 受到战斗伤害 | - |

#### 战斗条件

| 条件类型 | 中文名 | 参数 |
|----------|--------|------|
| Attacking | 正在攻击 | - |
| Blocking | 正在阻挡 | - |
| BlockedThisTurn | 本回合被阻挡 | - |
| WasBlocked | 被阻挡过 | - |

#### 连锁条件

| 条件类型 | 中文名 | 参数 |
|----------|--------|------|
| StackHasEffects | 栈上有效果 | - |
| StackEmpty | 栈为空 | - |
| HasPriority | 拥有优先权 | - |

#### 复合条件

| 条件类型 | 中文名 | 说明 |
|----------|--------|------|
| And | 满足所有条件 | 逻辑与 |
| Or | 满足任一条件 | 逻辑或 |
| Not | 条件取反 | 逻辑非 |
| Custom | 自定义条件 | 通过ID引用 |

---

## 五、数据存储

### 存储路径

```
Application.persistentDataPath/Effects/{effectId}.json
```

### 存储管理器 (EffectDefinitionStorage)

```csharp
// 单例访问
EffectDefinitionStorage.Instance

// 主要方法
SaveEffectAsync(EffectDefinitionData)  // 保存单个效果
LoadAllEffectsAsync()                  // 加载所有效果
LoadEffectAsync(string id)             // 加载指定效果
DeleteEffectAsync(string id)           // 删除效果
ExportToJson(EffectDefinitionData)     // 导出为JSON
ImportFromJson(string json)            // 从JSON导入
GenerateEffectId()                     // 生成唯一ID
```

### JSON 结构示例

```json
{
  "Id": "effect_12345_abc12345",
  "DisplayName": "火球术",
  "Description": "造成3点伤害",
  "BaseSpeed": 0,
  "ActivationType": 0,
  "TriggerTiming": 0,
  "IsOptional": false,
  "Duration": 0,
  "Cost": {
    "ElementCosts": [
      { "ManaType": 1, "Amount": 2 }
    ],
    "ResourceCosts": [],
    "PrepayCosts": []
  },
  "TargetSelector": {
    "PrimaryTarget": 6,
    "MinTargets": 1,
    "MaxTargets": 1,
    "Optional": false
  },
  "Effects": [
    { "Type": 0, "Value": 3, "Value2": 0 }
  ],
  "ActivationConditions": [],
  "Tags": []
}
```

---



