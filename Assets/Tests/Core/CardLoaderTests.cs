using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CardCore.Tests
{
    /// <summary>
    /// CardLoader 单元测试
    /// 验证 JSON 解析、卡牌构建、关键词注入、卡组构建
    /// </summary>
    [TestFixture]
    public class CardLoaderTests
    {
        private const string TestCardsJson = @"{
            ""cards"": [
                {
                    ""id"": ""TEST_RED_001"",
                    ""cardName"": ""火焰小鬼"",
                    ""supertype"": ""Creature"",
                    ""power"": 2,
                    ""life"": 1,
                    ""costList"": [{ ""manaType"": 1, ""amount"": 1 }],
                    ""keywords"": [""Charge""],
                    ""tags"": [""Test"", ""Red""]
                },
                {
                    ""id"": ""TEST_BLUE_001"",
                    ""cardName"": ""幻影间谍"",
                    ""supertype"": ""Creature"",
                    ""power"": 1,
                    ""life"": 2,
                    ""costList"": [{ ""manaType"": 2, ""amount"": 1 }],
                    ""keywords"": [""Stealth"", ""Flying""],
                    ""tags"": [""Test"", ""Blue""]
                },
                {
                    ""id"": ""TEST_GREEN_001"",
                    ""cardName"": ""林地幼兽"",
                    ""supertype"": ""Creature"",
                    ""power"": 1,
                    ""life"": 3,
                    ""costList"": [{ ""manaType"": 3, ""amount"": 1 }],
                    ""keywords"": [""Lifesteal""],
                    ""tags"": [""Test"", ""Green""]
                },
                {
                    ""id"": ""TEST_GRAY_001"",
                    ""cardName"": ""铁壁傀儡"",
                    ""supertype"": ""Creature"",
                    ""power"": 2,
                    ""life"": 4,
                    ""costList"": [{ ""manaType"": 0, ""amount"": 3 }],
                    ""keywords"": [""Armor""],
                    ""tags"": [""Test"", ""Gray""]
                }
            ],
            ""deckConfig"": {
                ""copiesPerCard"": 3
            }
        }";

        private const string KeywordsJson = @"{
            ""keywords"": [
                {
                    ""id"": ""Charge"",
                    ""nameZh"": ""冲锋"",
                    ""nameEn"": ""Charge"",
                    ""color"": ""Red"",
                    ""description"": ""召唤当回合可以攻击"",
                    ""isPassive"": true,
                    ""atomicEffect"": ""GrantHaste""
                },
                {
                    ""id"": ""Flying"",
                    ""nameZh"": ""飞行"",
                    ""nameEn"": ""Flying"",
                    ""color"": ""Blue"",
                    ""description"": ""只能被飞行或阻断飞行的生物阻挡"",
                    ""isPassive"": true,
                    ""atomicEffect"": ""GrantFlying""
                },
                {
                    ""id"": ""Lifesteal"",
                    ""nameZh"": ""吸血"",
                    ""nameEn"": ""Lifesteal"",
                    ""color"": ""Green"",
                    ""description"": ""造成的伤害回复等量生命"",
                    ""isPassive"": true,
                    ""atomicEffect"": ""GrantLifesteal""
                },
                {
                    ""id"": ""Armor"",
                    ""nameZh"": ""坚韧"",
                    ""nameEn"": ""Armor"",
                    ""color"": ""Green"",
                    ""description"": ""受到的伤害减少1点"",
                    ""isPassive"": true,
                    ""atomicEffect"": ""GrantArmor""
                }
            ]
        }";

        #region JSON 解析测试

        [Test]
        public void LoadCardsFromText_ParsesAllCards()
        {
            var cards = CardLoader.LoadCardsFromText(TestCardsJson);
            Assert.AreEqual(4, cards.Count);
        }

        [Test]
        public void LoadCardsFromText_ParsesCardBasicProperties()
        {
            var cards = CardLoader.LoadCardsFromText(TestCardsJson);
            var fireImp = cards[0];

            Assert.AreEqual("TEST_RED_001", fireImp.ID);
            Assert.AreEqual("火焰小鬼", fireImp.CardName);
            Assert.AreEqual(Cardtype.Creature, fireImp.Supertype);
        }

        [Test]
        public void LoadCardsFromText_ParsesCombatStats()
        {
            var cards = CardLoader.LoadCardsFromText(TestCardsJson);
            var fireImp = cards[0];

            Assert.AreEqual(2, fireImp.Power);
            Assert.AreEqual(1, fireImp.Life);
        }

        [Test]
        public void LoadCardsFromText_ParsesCost()
        {
            var cards = CardLoader.LoadCardsFromText(TestCardsJson);
            var fireImp = cards[0];

            Assert.IsNotNull(fireImp.Cost);
            Assert.AreEqual(1, fireImp.Cost.Count);
            Assert.IsTrue(fireImp.Cost.ContainsKey((int)ManaType.Red));
            Assert.AreEqual(1f, fireImp.Cost[(int)ManaType.Red]);
        }

        [Test]
        public void LoadCardsFromText_ParsesMultiColorCost()
        {
            var cards = CardLoader.LoadCardsFromText(TestCardsJson);
            // 幻影间谍: 1蓝
            var phantom = cards[1];
            Assert.AreEqual(1f, phantom.Cost[(int)ManaType.Blue]);
        }

        [Test]
        public void LoadCardsFromText_ParsesGrayCost()
        {
            var cards = CardLoader.LoadCardsFromText(TestCardsJson);
            var golem = cards[3]; // 铁壁傀儡: 3灰
            Assert.AreEqual(3f, golem.Cost[(int)ManaType.Gray]);
        }

        [Test]
        public void LoadCardsFromText_ParsesKeywords()
        {
            var cards = CardLoader.LoadCardsFromText(TestCardsJson);

            Assert.Contains("Charge", cards[0].Keywords);
            Assert.Contains("Stealth", cards[1].Keywords);
            Assert.Contains("Flying", cards[1].Keywords);
            Assert.Contains("Lifesteal", cards[2].Keywords);
            Assert.Contains("Armor", cards[3].Keywords);
        }

        [Test]
        public void LoadCardsFromText_ParsesTags()
        {
            var cards = CardLoader.LoadCardsFromText(TestCardsJson);
            Assert.Contains("Test", cards[0].Tags);
            Assert.Contains("Red", cards[0].Tags);
        }

        #endregion

        #region CardData → CardWrapper 转换测试

        [Test]
        public void ToCardInstances_CreatesCardWrappers()
        {
            var cardsData = CardLoader.LoadCardsFromText(TestCardsJson);
            var instances = CardLoader.ToCardInstances(cardsData);

            Assert.AreEqual(4, instances.Count);
            Assert.IsInstanceOf<CardWrapper>(instances[0]);
        }

        [Test]
        public void ToCardInstances_PreservesName()
        {
            var cardsData = CardLoader.LoadCardsFromText(TestCardsJson);
            var instances = CardLoader.ToCardInstances(cardsData);

            var name = (instances[0] as IHasName)?.CardName;
            Assert.AreEqual("火焰小鬼", name);
        }

        [Test]
        public void ToCardInstances_PreservesCombatStats()
        {
            var cardsData = CardLoader.LoadCardsFromText(TestCardsJson);
            var instances = CardLoader.ToCardInstances(cardsData);

            var power = (instances[0] as IHasPower)?.Power;
            var life = (instances[0] as IHasLife)?.Life;
            Assert.AreEqual(2, power);
            Assert.AreEqual(1, life);
        }

        [Test]
        public void ToCardInstances_InjectsKeywords()
        {
            var cardsData = CardLoader.LoadCardsFromText(TestCardsJson);
            var instances = CardLoader.ToCardInstances(cardsData);

            var keywords = (instances[0] as IHasKeywords)?.Keywords;
            Assert.IsNotNull(keywords);
            Assert.Contains("Charge", keywords);
        }

        [Test]
        public void ToCardInstances_InjectsMultipleKeywords()
        {
            var cardsData = CardLoader.LoadCardsFromText(TestCardsJson);
            var instances = CardLoader.ToCardInstances(cardsData);

            var keywords = (instances[1] as IHasKeywords)?.Keywords;
            Assert.IsNotNull(keywords);
            Assert.AreEqual(2, keywords.Count);
            Assert.Contains("Stealth", keywords);
            Assert.Contains("Flying", keywords);
        }

        [Test]
        public void ToCardInstances_CardWithoutKeywords_HasEmptySet()
        {
            var json = @"{
                ""cards"": [{
                    ""id"": ""PLAIN"",
                    ""cardName"": ""白板"",
                    ""supertype"": ""Creature"",
                    ""power"": 1,
                    ""life"": 1,
                    ""costList"": [{ ""manaType"": 0, ""amount"": 1 }],
                    ""keywords"": [],
                    ""tags"": []
                }],
                ""deckConfig"": { ""copiesPerCard"": 1 }
            }";

            var cardsData = CardLoader.LoadCardsFromText(json);
            var instances = CardLoader.ToCardInstances(cardsData);

            var keywords = (instances[0] as IHasKeywords)?.Keywords;
            Assert.IsNotNull(keywords);
            Assert.AreEqual(0, keywords.Count);
        }

        [Test]
        public void ToCardInstances_PreservesCost()
        {
            var cardsData = CardLoader.LoadCardsFromText(TestCardsJson);
            var instances = CardLoader.ToCardInstances(cardsData);

            var cost = (instances[0] as IHasCost)?.Cost;
            Assert.IsNotNull(cost);
            Assert.AreEqual(1f, cost[(int)ManaType.Red]);
        }

        #endregion

        #region 卡组构建测试

        [Test]
        public void BuildDeck_CorrectTotalCount()
        {
            var cardsData = CardLoader.LoadCardsFromText(TestCardsJson);
            var deck = CardLoader.BuildDeck(cardsData, 3);

            // 4 种卡 × 3 张 = 12
            Assert.AreEqual(12, deck.Count);
        }

        [Test]
        public void BuildDeck_EachCardHasCorrectCopies()
        {
            var cardsData = CardLoader.LoadCardsFromText(TestCardsJson);
            var deck = CardLoader.BuildDeck(cardsData, 3);

            var fireImpCount = deck.Count(c =>
            {
                var name = (c as IHasName)?.CardName;
                return name == "火焰小鬼";
            });
            Assert.AreEqual(3, fireImpCount);
        }

        [Test]
        public void BuildDeck_SingleCopy()
        {
            var cardsData = CardLoader.LoadCardsFromText(TestCardsJson);
            var deck = CardLoader.BuildDeck(cardsData, 1);

            Assert.AreEqual(4, deck.Count);
        }

        [Test]
        public void BuildTestDeckFromText_Integration()
        {
            var deck = CardLoader.BuildTestDeckFromText(TestCardsJson, 3);
            Assert.AreEqual(12, deck.Count);

            // 每张卡都应该有 IHasKeywords 接口
            foreach (var card in deck)
            {
                Assert.IsTrue(card is IHasKeywords, $"卡牌 {(card as IHasName)?.CardName} 缺少 IHasKeywords");
            }
        }

        #endregion

        #region 关键词查询测试

        [Test]
        public void CardWrapper_HasKeyword_ReturnsTrue()
        {
            var cardsData = CardLoader.LoadCardsFromText(TestCardsJson);
            var instances = CardLoader.ToCardInstances(cardsData);

            var kw = instances[0] as IHasKeywords;
            Assert.IsTrue(kw.HasKeyword("Charge"));
            Assert.IsFalse(kw.HasKeyword("Flying"));
        }

        [Test]
        public void CardWrapper_AddKeyword_Works()
        {
            var cardsData = CardLoader.LoadCardsFromText(TestCardsJson);
            var instances = CardLoader.ToCardInstances(cardsData);

            var kw = instances[0] as IHasKeywords;
            kw.AddKeyword("Flying");
            Assert.IsTrue(kw.HasKeyword("Flying"));
        }

        [Test]
        public void CardWrapper_RemoveKeyword_Works()
        {
            var cardsData = CardLoader.LoadCardsFromText(TestCardsJson);
            var instances = CardLoader.ToCardInstances(cardsData);

            var kw = instances[0] as IHasKeywords;
            Assert.IsTrue(kw.HasKeyword("Charge"));
            kw.RemoveKeyword("Charge");
            Assert.IsFalse(kw.HasKeyword("Charge"));
        }

        #endregion

        #region 边界条件测试

        [Test]
        public void LoadCardsFromText_EmptyCards_ReturnsEmptyList()
        {
            var json = @"{ ""cards"": [], ""deckConfig"": { ""copiesPerCard"": 3 } }";
            var cards = CardLoader.LoadCardsFromText(json);
            Assert.AreEqual(0, cards.Count);
        }

        [Test]
        public void BuildDeck_EmptyList_ReturnsEmptyDeck()
        {
            var deck = CardLoader.BuildDeck(new List<CardData>(), 3);
            Assert.AreEqual(0, deck.Count);
        }

        [Test]
        public void LoadCardsFromText_NullKeywords_HandledGracefully()
        {
            var json = @"{
                ""cards"": [{
                    ""id"": ""NO_KW"",
                    ""cardName"": ""无关键词"",
                    ""supertype"": ""Creature"",
                    ""power"": 1,
                    ""life"": 1,
                    ""costList"": [],
                    ""keywords"": [],
                    ""tags"": []
                }],
                ""deckConfig"": { ""copiesPerCard"": 1 }
            }";

            var cards = CardLoader.LoadCardsFromText(json);
            Assert.AreEqual(1, cards.Count);
            Assert.AreEqual(0, cards[0].Keywords.Count);
        }

        #endregion
    }
}
