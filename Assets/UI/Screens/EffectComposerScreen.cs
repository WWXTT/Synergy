using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 效果合成界面（Phase 2-3 精修）—— 左 60% 效果序列 / 右 40% 上下文面板。
    ///
    ///   左：效果元信息 + 竖排步骤卡序列（原子 kind=0 / 分支 kind=1）。点击步骤卡选中。
    ///   右：随选中态切换 ——
    ///       未选中 → 可添加内容目录（颜色过滤 chip + 原子效果 + 关键词），点击追加为新步骤。
    ///       选中原子步 → 参数 + 目标(类型/filter/数量/范围) + 代价子列表。
    ///       选中分支步 → 条件编辑 + then/else 原子子序列。
    ///   存/读：EffectLibrarySerializer（按功能内容哈希去重）。
    ///
    /// 注：目标/代价本阶段仅持久化 + UI 还原；执行（目标弹窗/代价支付）留 Phase 4。
    /// </summary>
    public sealed class EffectComposerScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/EffectComposer";

        // 当前编排中的效果图（内存模型，存盘前的唯一真相）。
        private EffectGraphData _graph = new EffectGraphData("新效果");

        private ScrollView _stepsList;
        private ScrollView _contextPane;
        private Label _contextTitle;
        private Label _toast;
        private TextField _nameField;
        private DropdownField _timingDropdown;
        private DropdownField _activationDropdown;
        private DropdownField _graphsDropdown;

        private List<TriggerTiming> _timings;

        // 右侧上下文状态：选中的步骤索引（-1 = 未选中，显示目录）+ 目录颜色过滤。
        private int _selectedStepIndex = -1;
        private UIColor _catalogFilter = UIColor.All;

        // 发动方式：0=强制 1=自动 2=主动。
        private static readonly string[] ActivationNames = { "强制", "自动", "主动" };

        public override void OnEnter()
        {
            _stepsList = Q<ScrollView>("list-steps");
            _contextPane = Q<ScrollView>("context-pane");
            _contextTitle = Q<Label>("lbl-context-title");
            _toast = Q<Label>("lbl-toast");
            _nameField = Q<TextField>("field-effect-name");
            _timingDropdown = Q<DropdownField>("dropdown-timing");
            _activationDropdown = Q<DropdownField>("dropdown-activation");
            _graphsDropdown = Q<DropdownField>("dropdown-graphs");

            UIBinder.BindButton(Root, "btn-back", () => Manager.Back());
            UIBinder.BindButton(Root, "btn-save", OnSave);
            UIBinder.BindButton(Root, "btn-load", OnLoad);
            UIBinder.BindButton(Root, "btn-add-atomic", () => AddAtomicStep(FirstAtomicType()));
            UIBinder.BindButton(Root, "btn-add-branch", AddBranchStep);

            UIBinder.BindField(_nameField, _graph.name, v => _graph.name = v);

            BuildTimingDropdown();
            BuildActivationDropdown();
            RefreshSteps();
            RefreshContext();
            RefreshGraphsDropdown();
        }

        // ---------- 元信息下拉 ----------
        private void BuildTimingDropdown()
        {
            _timings = Enum.GetValues(typeof(TriggerTiming)).Cast<TriggerTiming>().ToList();
            _timingDropdown.choices = _timings.Select(t => t.ToString()).ToList();
            int current = _timings.IndexOf((TriggerTiming)_graph.header.TriggerTiming);
            _timingDropdown.index = current < 0 ? 0 : current;
            _timingDropdown.RegisterValueChangedCallback(_ =>
            {
                int idx = _timingDropdown.index;
                if (idx >= 0 && idx < _timings.Count)
                {
                    _graph.header.TriggerTiming = (int)_timings[idx];
                }
            });
        }

        private void BuildActivationDropdown()
        {
            _activationDropdown.choices = ActivationNames.ToList();
            int act = _graph.header.ActivationType;
            _activationDropdown.index = act >= 0 && act < ActivationNames.Length ? act : 0;
            _activationDropdown.RegisterValueChangedCallback(_ =>
                _graph.header.ActivationType = _activationDropdown.index);
        }

        // ---------- 步骤增删 ----------
        private void AddAtomicStep(AtomicEffectType type)
        {
            _graph.steps.Add(new EffectStepData
            {
                kind = 0,
                atomic = new AtomicEffectEntry { EffectType = type.ToString(), Value = 1 },
            });
            _selectedStepIndex = _graph.steps.Count - 1;
            RefreshSteps();
            RefreshContext();
        }

        // 关键词作为原子追加：EffectType = 映射的 Grant* 原子，默认 Self 目标，StringValue = 关键词 id。
        private void AddKeywordStep(KeywordCatalogEntry kw)
        {
            _graph.steps.Add(new EffectStepData
            {
                kind = 0,
                atomic = new AtomicEffectEntry
                {
                    EffectType = kw.AtomicEffect,
                    Value = 1,
                    StringValue = kw.Id,
                    TargetTypeOverride = (int)EffectTargetType.Self,
                },
            });
            _selectedStepIndex = _graph.steps.Count - 1;
            RefreshSteps();
            RefreshContext();
        }

        private void AddBranchStep()
        {
            _graph.steps.Add(new EffectStepData
            {
                kind = 1,
                condition = new ActivationConditionData(),
                thenSteps = new List<AtomicEffectEntry>(),
                elseSteps = new List<AtomicEffectEntry>(),
            });
            _selectedStepIndex = _graph.steps.Count - 1;
            RefreshSteps();
            RefreshContext();
        }

        private void RemoveStepAt(int index)
        {
            if (index >= 0 && index < _graph.steps.Count)
            {
                _graph.steps.RemoveAt(index);
                if (_selectedStepIndex == index)
                {
                    _selectedStepIndex = -1;
                }
                else if (_selectedStepIndex > index)
                {
                    _selectedStepIndex--;
                }
                RefreshSteps();
                RefreshContext();
            }
        }

        private void MoveStep(int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || index >= _graph.steps.Count || target < 0 || target >= _graph.steps.Count)
            {
                return;
            }
            (_graph.steps[index], _graph.steps[target]) = (_graph.steps[target], _graph.steps[index]);
            if (_selectedStepIndex == index)
            {
                _selectedStepIndex = target;
            }
            else if (_selectedStepIndex == target)
            {
                _selectedStepIndex = index;
            }
            RefreshSteps();
            RefreshContext();
        }

        private void SelectStep(int index)
        {
            _selectedStepIndex = _selectedStepIndex == index ? -1 : index;
            RefreshSteps();
            RefreshContext();
        }

        // ---------- 左：渲染效果序列（步骤卡为可选中摘要） ----------
        private void RefreshSteps()
        {
            _stepsList.Clear();
            for (int i = 0; i < _graph.steps.Count; i++)
            {
                var index = i;
                _stepsList.Add(MakeStepSummary(_graph.steps[i], index));
            }
        }

        // 步骤摘要卡：序号 + 标题 + 上移/下移/删除；点击选中（高亮）。
        private VisualElement MakeStepSummary(EffectStepData step, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("step-card");
            card.AddToClassList("list-row");
            if (index == _selectedStepIndex)
            {
                card.AddToClassList("list-row--selected");
            }

            var label = new Label($"#{index + 1}  {StepSummaryText(step)}");
            label.AddToClassList("list-row__name");
            card.Add(label);

            var up = new Button(() => MoveStep(index, -1)) { text = "↑" };
            up.AddToClassList("btn");
            up.AddToClassList("btn--mini");
            card.Add(up);

            var down = new Button(() => MoveStep(index, 1)) { text = "↓" };
            down.AddToClassList("btn");
            down.AddToClassList("btn--mini");
            card.Add(down);

            var del = new Button(() => RemoveStepAt(index)) { text = "删除" };
            del.AddToClassList("btn");
            del.AddToClassList("btn--mini");
            del.AddToClassList("btn--danger");
            card.Add(del);

            label.RegisterCallback<ClickEvent>(_ => SelectStep(index));
            return card;
        }

        private static string StepSummaryText(EffectStepData step)
        {
            if (step.kind == 1)
            {
                var c = step.condition;
                var ct = c != null ? (ConditionType)c.Type : ConditionType.MinCardsInHand;
                return $"条件分支（{ct}）";
            }
            var a = step.atomic;
            if (a == null || string.IsNullOrEmpty(a.EffectType))
            {
                return "原子效果";
            }
            string display = Enum.TryParse<AtomicEffectType>(a.EffectType, out var t)
                ? DisplayNameOf(t)
                : a.EffectType;
            return $"{display} x{a.Value}";
        }

        private static string DisplayNameOf(AtomicEffectType type)
        {
            var cfg = AtomicEffectTable.GetByType(type);
            return cfg != null && !string.IsNullOrEmpty(cfg.DisplayName)
                ? $"{cfg.DisplayName} ({type})"
                : type.ToString();
        }

        private static AtomicEffectType FirstAtomicType()
        {
            return Enum.GetValues(typeof(AtomicEffectType)).Cast<AtomicEffectType>().First();
        }

        // ---------- 右：上下文面板（随选中态切换） ----------
        private void RefreshContext()
        {
            _contextPane.Clear();

            if (_selectedStepIndex < 0 || _selectedStepIndex >= _graph.steps.Count)
            {
                _contextTitle.text = "可添加内容（点击追加到序列）";
                BuildCatalog();
                return;
            }

            var step = _graph.steps[_selectedStepIndex];
            if (step.kind == 1)
            {
                _contextTitle.text = $"编辑分支 #{_selectedStepIndex + 1}";
                BuildBranchEditor(step);
            }
            else
            {
                _contextTitle.text = $"编辑原子 #{_selectedStepIndex + 1}";
                BuildAtomicEditorPane(step);
            }
        }

        // ----- 未选中：可添加内容目录（颜色过滤 + 原子 + 关键词） -----
        private void BuildCatalog()
        {
            _contextPane.Add(MakeColorFilterBar(_catalogFilter, c =>
            {
                _catalogFilter = c;
                RefreshContext();
            }));

            var atomicHeader = new Label("原子效果");
            atomicHeader.AddToClassList("panel__header");
            _contextPane.Add(atomicHeader);
            foreach (var type in Enum.GetValues(typeof(AtomicEffectType)).Cast<AtomicEffectType>())
            {
                if (!ColorFilter.Matches(_catalogFilter, ColorFilter.OfAtomic(type)))
                {
                    continue;
                }
                var captured = type;
                _contextPane.Add(MakeCatalogRow(DisplayNameOf(type), ColorFilter.OfAtomic(type),
                    () => AddAtomicStep(captured)));
            }

            var kwHeader = new Label("关键词（默认 Self）");
            kwHeader.AddToClassList("panel__header");
            _contextPane.Add(kwHeader);
            foreach (var kw in KeywordCatalog.LoadAll())
            {
                if (!ColorFilter.Matches(_catalogFilter, kw.Color))
                {
                    continue;
                }
                var captured = kw;
                _contextPane.Add(MakeCatalogRow(kw.DisplayName, kw.Color, () => AddKeywordStep(captured)));
            }
        }

        // 颜色过滤条：全部/红/蓝/绿/灰 chip。
        private VisualElement MakeColorFilterBar(UIColor active, Action<UIColor> onPick)
        {
            var bar = new VisualElement();
            bar.AddToClassList("toolbar");
            bar.AddToClassList("filter-bar");
            foreach (UIColor color in Enum.GetValues(typeof(UIColor)))
            {
                var captured = color;
                var chip = new Button(() => onPick(captured)) { text = ColorFilter.DisplayName(color) };
                chip.AddToClassList("chip");
                chip.AddToClassList(ChipClass(color));
                if (color == active)
                {
                    chip.AddToClassList("chip--active");
                }
                bar.Add(chip);
            }
            return bar;
        }

        private static string ChipClass(UIColor color)
        {
            return color switch
            {
                UIColor.Red => "chip--red",
                UIColor.Blue => "chip--blue",
                UIColor.Green => "chip--green",
                UIColor.Gray => "chip--gray",
                _ => "chip--all",
            };
        }

        private VisualElement MakeCatalogRow(string text, UIColor color, Action onClick)
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");

            var dot = new VisualElement();
            dot.AddToClassList("color-dot");
            dot.AddToClassList(ChipClass(color));
            row.Add(dot);

            var name = new Label(text);
            name.AddToClassList("list-row__name");
            row.Add(name);

            row.RegisterCallback<ClickEvent>(_ => onClick());
            return row;
        }

        // ----- 选中原子步：参数 + 目标 + 代价 -----
        private void BuildAtomicEditorPane(EffectStepData step)
        {
            step.atomic ??= new AtomicEffectEntry();
            var atomic = step.atomic;

            var paramHeader = new Label("参数");
            paramHeader.AddToClassList("panel__header");
            _contextPane.Add(paramHeader);
            _contextPane.Add(MakeAtomicEditor(atomic, refreshSummaryOnTypeChange: true));

            var targetHeader = new Label("目标");
            targetHeader.AddToClassList("panel__header");
            _contextPane.Add(targetHeader);
            _contextPane.Add(MakeTargetEditor(atomic));

            var costHeader = new Label("代价");
            costHeader.AddToClassList("panel__header");
            _contextPane.Add(costHeader);
            _contextPane.Add(MakeCostList());
        }

        // 原子效果参数编辑器（分支 then/else 子项复用）。
        private VisualElement MakeAtomicEditor(AtomicEffectEntry atomic, bool refreshSummaryOnTypeChange = false)
        {
            atomic.EffectType ??= FirstAtomicType().ToString();

            var rowA = new VisualElement();
            rowA.AddToClassList("toolbar");

            var typeDropdown = new DropdownField("效果");
            var typeNames = Enum.GetValues(typeof(AtomicEffectType)).Cast<AtomicEffectType>()
                .Select(t => t.ToString()).ToList();
            typeDropdown.choices = typeNames;
            typeDropdown.AddToClassList("text-input");
            int curType = typeNames.IndexOf(atomic.EffectType);
            typeDropdown.index = curType >= 0 ? curType : 0;
            if (curType < 0)
            {
                atomic.EffectType = typeNames[0];
            }
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                atomic.EffectType = evt.newValue;
                if (refreshSummaryOnTypeChange)
                {
                    RefreshSteps();
                }
            });
            rowA.Add(typeDropdown);

            rowA.Add(MakeIntField("主值", atomic.Value, v =>
            {
                atomic.Value = v;
                if (refreshSummaryOnTypeChange)
                {
                    RefreshSteps();
                }
            }));
            rowA.Add(MakeIntField("副值", atomic.Value2, v => atomic.Value2 = v));

            var rowB = new VisualElement();
            rowB.AddToClassList("toolbar");
            rowB.Add(MakeIntField("法力", atomic.ManaTypeParam, v => atomic.ManaTypeParam = v));
            rowB.Add(MakeIntField("分区", atomic.ZoneParam, v => atomic.ZoneParam = v));
            rowB.Add(MakeIntField("持续", atomic.Duration, v => atomic.Duration = v));

            var strField = new TextField("字符串");
            strField.AddToClassList("text-input");
            strField.SetValueWithoutNotify(atomic.StringValue ?? "");
            strField.RegisterValueChangedCallback(evt => atomic.StringValue = evt.newValue);
            rowB.Add(strField);

            var wrap = new VisualElement();
            wrap.Add(rowA);
            wrap.Add(rowB);
            return wrap;
        }

        // 目标编辑器：TargetType / TargetFilter(token 串) / TargetCount / TargetScope，对应 4 个覆盖字段。
        private VisualElement MakeTargetEditor(AtomicEffectEntry atomic)
        {
            var wrap = new VisualElement();

            var rowA = new VisualElement();
            rowA.AddToClassList("toolbar");

            var targetNames = Enum.GetValues(typeof(EffectTargetType)).Cast<EffectTargetType>()
                .Select(t => t.ToString()).ToList();
            var typeDropdown = new DropdownField("作用对象");
            typeDropdown.choices = targetNames;
            typeDropdown.AddToClassList("text-input");
            int ti = atomic.TargetTypeOverride;
            typeDropdown.index = ti >= 0 && ti < targetNames.Count ? ti : (int)EffectTargetType.Self;
            atomic.TargetTypeOverride = typeDropdown.index;
            typeDropdown.RegisterValueChangedCallback(_ => atomic.TargetTypeOverride = typeDropdown.index);
            rowA.Add(typeDropdown);

            var scopeNames = Enum.GetValues(typeof(EffectTargetScope)).Cast<EffectTargetScope>()
                .Select(s => s.ToString()).ToList();
            var scopeDropdown = new DropdownField("范围");
            scopeDropdown.choices = scopeNames;
            scopeDropdown.AddToClassList("text-input");
            int si = atomic.TargetScopeOverride;
            scopeDropdown.index = si >= 0 && si < scopeNames.Count ? si : (int)EffectTargetScope.Single;
            atomic.TargetScopeOverride = scopeDropdown.index;
            scopeDropdown.RegisterValueChangedCallback(_ => atomic.TargetScopeOverride = scopeDropdown.index);
            rowA.Add(scopeDropdown);
            wrap.Add(rowA);

            var rowB = new VisualElement();
            rowB.AddToClassList("toolbar");

            // TargetCount: -2=用配置, -1=任意, 0=全部, >0=数量。用普通整数框，提示语义。
            if (atomic.TargetCountOverride == -2)
            {
                atomic.TargetCountOverride = 1;
            }
            rowB.Add(MakeIntField("数量(-1任意/0全部)", atomic.TargetCountOverride,
                v => atomic.TargetCountOverride = v));

            var filterField = new TextField("过滤");
            filterField.AddToClassList("text-input");
            filterField.SetValueWithoutNotify(atomic.TargetFilterOverride ?? "");
            filterField.RegisterValueChangedCallback(evt => atomic.TargetFilterOverride = evt.newValue);
            rowB.Add(filterField);
            wrap.Add(rowB);

            var hint = new Label("filter token：Creature/Player/Untapped/Tapped/Damaged/Friendly/Enemy/Power>N/Life<=N（逗号分隔）");
            hint.AddToClassList("hint");
            wrap.Add(hint);

            return wrap;
        }

        // 代价子列表：作用于效果整体（_graph.header.Costs），每条对应一个 CostEntry。
        private static readonly CostType[] CostTypes =
        {
            CostType.ElementConsume, CostType.DiscardCard, CostType.LifePayment,
            CostType.Sleep, CostType.SummonMaterial,
        };

        private VisualElement MakeCostList()
        {
            _graph.header.Costs ??= new List<CostEntry>();
            var costs = _graph.header.Costs;

            var container = new VisualElement();
            container.AddToClassList("sub-list");

            void Rebuild()
            {
                container.Clear();
                for (int i = 0; i < costs.Count; i++)
                {
                    var idx = i;
                    container.Add(MakeCostRow(costs[idx], () => { costs.RemoveAt(idx); Rebuild(); }));
                }
                var add = new Button(() =>
                {
                    costs.Add(new CostEntry { CostType = (int)CostType.ElementConsume, Value = 1 });
                    Rebuild();
                }) { text = "+ 代价" };
                add.AddToClassList("btn");
                container.Add(add);
            }

            Rebuild();
            return container;
        }

        // 单条代价行：CostType 下拉 + 按类型显示参数（元素消耗→法力+数量；弃牌/生命/素材→数量；沉睡→回合）。
        private VisualElement MakeCostRow(CostEntry cost, Action onDelete)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("toolbar");

            var typeDropdown = new DropdownField();
            typeDropdown.choices = CostTypes.Select(c => CardCostCalculator.CostTypeName((int)c)).ToList();
            typeDropdown.AddToClassList("text-input");
            int ci = Array.IndexOf(CostTypes, (CostType)cost.CostType);
            typeDropdown.index = ci >= 0 ? ci : 0;

            var paramHost = new VisualElement();
            paramHost.AddToClassList("toolbar");

            void RebuildParams()
            {
                paramHost.Clear();
                switch ((CostType)cost.CostType)
                {
                    case CostType.ElementConsume:
                        paramHost.Add(MakeIntField("法力色", cost.ManaType, v => cost.ManaType = v));
                        paramHost.Add(MakeIntField("数量", cost.Value, v => cost.Value = v));
                        break;
                    case CostType.Sleep:
                        paramHost.Add(MakeIntField("回合", cost.TurnDuration, v => cost.TurnDuration = v));
                        break;
                    default: // DiscardCard / LifePayment / SummonMaterial
                        paramHost.Add(MakeIntField("数量", cost.Value, v => cost.Value = v));
                        break;
                }
            }

            typeDropdown.RegisterValueChangedCallback(_ =>
            {
                int idx = typeDropdown.index;
                if (idx >= 0 && idx < CostTypes.Length)
                {
                    cost.CostType = (int)CostTypes[idx];
                    RebuildParams();
                }
            });

            wrap.Add(typeDropdown);
            wrap.Add(paramHost);

            var del = new Button(onDelete) { text = "x" };
            del.AddToClassList("btn");
            del.AddToClassList("btn--mini");
            del.AddToClassList("btn--danger");
            wrap.Add(del);

            RebuildParams();
            return wrap;
        }

        // ----- 选中分支步：条件 + then/else 原子子序列 -----
        private void BuildBranchEditor(EffectStepData step)
        {
            step.condition ??= new ActivationConditionData();
            step.thenSteps ??= new List<AtomicEffectEntry>();
            step.elseSteps ??= new List<AtomicEffectEntry>();

            var condHeader = new Label("条件");
            condHeader.AddToClassList("panel__header");
            _contextPane.Add(condHeader);
            _contextPane.Add(MakeConditionEditor(step.condition));

            var thenHeader = new Label("成立（then）");
            thenHeader.AddToClassList("panel__header");
            _contextPane.Add(thenHeader);
            _contextPane.Add(MakeSubList(step.thenSteps));

            var elseHeader = new Label("否则（else）");
            elseHeader.AddToClassList("panel__header");
            _contextPane.Add(elseHeader);
            _contextPane.Add(MakeSubList(step.elseSteps));
        }

        private VisualElement MakeConditionEditor(ActivationConditionData cond)
        {
            var wrap = new VisualElement();

            var row = new VisualElement();
            row.AddToClassList("toolbar");

            var typeDropdown = new DropdownField("条件");
            var condNames = Enum.GetValues(typeof(ConditionType)).Cast<ConditionType>()
                .Select(c => c.ToString()).ToList();
            typeDropdown.choices = condNames;
            typeDropdown.AddToClassList("text-input");
            typeDropdown.index = cond.Type >= 0 && cond.Type < condNames.Count ? cond.Type : 0;
            typeDropdown.RegisterValueChangedCallback(_ =>
            {
                cond.Type = typeDropdown.index;
                RefreshSteps();
            });
            row.Add(typeDropdown);
            wrap.Add(row);

            var row2 = new VisualElement();
            row2.AddToClassList("toolbar");
            row2.Add(MakeIntField("值", cond.Value, v => cond.Value = v));
            row2.Add(MakeIntField("值2", cond.Value2, v => cond.Value2 = v));

            var negate = new Toggle("取反");
            negate.SetValueWithoutNotify(cond.Negate);
            negate.RegisterValueChangedCallback(evt => cond.Negate = evt.newValue);
            row2.Add(negate);
            wrap.Add(row2);

            return wrap;
        }

        // then/else 原子子序列：竖排 + 「+原子」按钮 + 每项可删（含目标编辑）。
        private VisualElement MakeSubList(List<AtomicEffectEntry> entries)
        {
            var container = new VisualElement();
            container.AddToClassList("sub-list");

            void Rebuild()
            {
                container.Clear();
                for (int i = 0; i < entries.Count; i++)
                {
                    var idx = i;
                    var entry = entries[i];
                    var item = new VisualElement();
                    item.AddToClassList("panel");
                    item.AddToClassList("sub-item");

                    var bar = new VisualElement();
                    bar.AddToClassList("toolbar");
                    var title = new Label("原子");
                    title.AddToClassList("list-row__name");
                    bar.Add(title);
                    var del = new Button(() => { entries.RemoveAt(idx); Rebuild(); }) { text = "x" };
                    del.AddToClassList("btn");
                    del.AddToClassList("btn--mini");
                    del.AddToClassList("btn--danger");
                    bar.Add(del);
                    item.Add(bar);

                    item.Add(MakeAtomicEditor(entry));
                    item.Add(MakeTargetEditor(entry));
                    container.Add(item);
                }

                var add = new Button(() =>
                {
                    entries.Add(new AtomicEffectEntry { EffectType = FirstAtomicType().ToString(), Value = 1 });
                    Rebuild();
                }) { text = "+ 原子" };
                add.AddToClassList("btn");
                container.Add(add);
            }

            Rebuild();
            return container;
        }

        private VisualElement MakeIntField(string label, int value, Action<int> onChanged)
        {
            var field = new IntegerField(label);
            field.AddToClassList("int-input");
            field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return field;
        }

        // ---------- 存/读 ----------
        private void OnSave()
        {
            if (string.IsNullOrWhiteSpace(_graph.name))
            {
                _graph.name = "新效果";
            }
            _graph.header.DisplayName = _graph.name;
            var path = EffectLibrarySerializer.Save(_graph);
            ShowToast(path == null ? "保存失败" : $"已保存：{_graph.name}");
            RefreshGraphsDropdown();
            if (_graphsDropdown.choices.Contains(_graph.name))
            {
                _graphsDropdown.SetValueWithoutNotify(_graph.name);
            }
        }

        private void OnLoad()
        {
            var name = _graphsDropdown.value;
            if (string.IsNullOrEmpty(name))
            {
                ShowToast("请先选择效果");
                return;
            }
            var loaded = EffectLibrarySerializer.Load(name);
            if (loaded == null)
            {
                ShowToast("读取失败");
                return;
            }
            _graph = loaded;
            _graph.header ??= new CardEffectData();
            _graph.steps ??= new List<EffectStepData>();

            _nameField.SetValueWithoutNotify(_graph.name);
            int idx = _timings.IndexOf((TriggerTiming)_graph.header.TriggerTiming);
            _timingDropdown.index = idx >= 0 ? idx : 0;
            int act = _graph.header.ActivationType;
            _activationDropdown.index = act >= 0 && act < ActivationNames.Length ? act : 0;

            _selectedStepIndex = -1;
            RefreshSteps();
            RefreshContext();
            ShowToast($"已载入：{_graph.name}");
        }

        private void RefreshGraphsDropdown()
        {
            var names = EffectLibrarySerializer.LoadAll().Select(g => g.name).ToList();
            _graphsDropdown.choices = names;
            if (names.Count > 0 && string.IsNullOrEmpty(_graphsDropdown.value))
            {
                _graphsDropdown.SetValueWithoutNotify(names[0]);
            }
        }

        private void ShowToast(string message)
        {
            _toast.text = message;
        }
    }
}
