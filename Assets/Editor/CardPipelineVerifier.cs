using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace CardCore.Editor
{
    /// <summary>
    /// 端到端验证：读配置 → 组卡 → 真实对局中打出并结算。
    /// 覆盖法术（火球术 DealDamage+DrawCard）与生物（古树守卫，配置驱动血量、可被伤害效果击杀）。
    /// </summary>
    public static class CardPipelineVerifier
    {
        private static int _pass;
        private static int _fail;

        [MenuItem("Tools/卡牌核心/端到端验证")]
        public static void RunVerification()
        {
            _pass = 0;
            _fail = 0;

            string jsonPath = Path.Combine(Application.dataPath, "Configs/TestCreatureCards.json");
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[Verify] 找不到测试卡配置: {jsonPath}");
                return;
            }

            var cardsData = CardLoader.LoadCardsFromText(File.ReadAllText(jsonPath));
            Debug.Log($"[Verify] 加载卡牌数据 {cardsData.Count} 张");

            var core = GameCore.Instance;
            var deck1 = CardLoader.BuildDeck(cardsData, 3);
            var deck2 = CardLoader.BuildDeck(cardsData, 3);
            core.InitGame(deck1, deck2);

            var p1 = core.Player1;
            var p2 = core.Player2;

            // 准备阶段 → 主阶段
            GameActions.SkipElementPool(core, p1);

            // 回合开始接线（2.1）：InitGame→StartGame→StartNewTurn 发布 TurnStartEvent，
            // GameCore 订阅后应已重置栈优先权持有者
            Assert(core.StackEngine.CurrentPriorityHolder != null,
                   $"回合开始接线生效（优先权持有者非空：{core.StackEngine.CurrentPriorityHolder?.Name}）");

            TestSpell(core, p1, p2, cardsData);
            TestCreature(core, p1, cardsData);

            Debug.Log($"[Verify] 完成 — PASS={_pass} FAIL={_fail}");
            if (_fail == 0)
                Debug.Log("[Verify] ✅ 整条链路贯通：读配置→组卡→对局结算");
            else
                Debug.LogError($"[Verify] ❌ 存在 {_fail} 项失败");
        }

        private static void TestSpell(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            var data = cardsData.FirstOrDefault(c => c.ID == "TEST_SPELL_RED_001");
            Assert(data != null, "火球术配置存在");
            if (data == null) return;
            Assert(data.Effects != null && data.Effects.Count > 0
                   && data.Effects[0].AtomicEffects != null
                   && data.Effects[0].AtomicEffects.Count == 2,
                   "火球术效果反序列化（DealDamage+DrawCard）");

            var fireball = new CardWrapper(data);
            fireball.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(fireball, Zone.Hand);
            core.ElementPool.GetPool(p1).AvailableMana[ManaType.Red] = 3;

            int lifeBefore = p2.Life;
            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;

            bool played = GameActions.PlayCard(core, p1, fireball,
                new List<Entity> { p2 });
            Assert(played, "火球术成功打出");

            Assert(p2.Life == lifeBefore - 4, $"对手 Life −4（{lifeBefore}→{p2.Life}）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1,
                   "抽牌生效（牌库 −1）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball),
                   "法术结算后入墓地");
        }

        private static void TestCreature(GameCore core, Player p1, List<CardData> cardsData)
        {
            var data = cardsData.FirstOrDefault(c => c.ID == "TEST_GREEN_003");
            Assert(data != null, "古树守卫配置存在");
            if (data == null) return;

            var creature = new CardWrapper(data);
            creature.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(creature, Zone.Hand);
            var pool = core.ElementPool.GetPool(p1);
            pool.AvailableMana[ManaType.Green] = 3;
            pool.AvailableMana[ManaType.Gray] = 1;

            bool played = GameActions.PlayCard(core, p1, creature);
            Assert(played, "古树守卫成功打出");
            Assert(core.ZoneManager.GetCards(p1, Zone.Battlefield).Contains(creature),
                   "生物进入战场");
            // 配置驱动血量：CardWrapper 同步 _life 前此处恒为 1
            Assert(creature.GetLife() == 5, $"血量来自配置（应为5，实际{creature.GetLife()}）");

            // 伤害效果击杀
            var killDef = new EffectDefinition
            {
                Id = "VERIFY_KILL",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance>
                {
                    new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 5 }
                }
            };
            var kill = new EffectInstance
            {
                Definition = killDef,
                Source = creature,
                Controller = p1,
                Targets = new List<Entity> { creature },
            };
            //core.StackEngine.GetExecutor().Execute(kill);

            Assert(creature.GetLife() <= 0, $"受伤后血量≤0（实际{creature.GetLife()}）");
            Assert(!creature.IsAlive, "生物被伤害效果击杀");
        }

        private static void Assert(bool condition, string label)
        {
            if (condition)
            {
                _pass++;
                Debug.Log($"[Verify] PASS — {label}");
            }
            else
            {
                _fail++;
                Debug.LogError($"[Verify] FAIL — {label}");
            }
        }
    }
}
