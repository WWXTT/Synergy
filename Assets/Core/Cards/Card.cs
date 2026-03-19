namespace CardCore
{
    /// <summary>
    /// 卡牌基类 - 继承自 Entity
    /// 其他属性通过接口形式按需添加
    /// </summary>
    public class Card : Entity
    {
        /// <summary>
        /// 卡牌唯一标识ID
        /// </summary>
        public string ID { get; set; }

        public Card() : base(createTimestamp: false) { }
    }
}
