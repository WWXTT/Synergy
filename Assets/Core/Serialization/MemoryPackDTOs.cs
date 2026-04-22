using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;

namespace CardCore.Serialization
{
    [MemoryPackable]
    public partial class SerializableCardData
    {
        [MemoryPackOrder(TagTable.CardData_ID)]
        public string ID;

        [MemoryPackOrder(TagTable.CardData_Supertype)]
        public int Supertype;

        [MemoryPackOrder(TagTable.CardData_CardName)]
        public string CardName;

        [MemoryPackOrder(TagTable.CardData_Illustration)]
        public string Illustration;

        [MemoryPackOrder(TagTable.CardData_Life)]
        public int? Life;

        [MemoryPackOrder(TagTable.CardData_Power)]
        public int? Power;

        [MemoryPackOrder(TagTable.CardData_Cost)]
        public CostEntryDTO[] Cost;

        [MemoryPackOrder(TagTable.CardData_Effects)]
        public SerializableEffectData[] Effects;

        [MemoryPackOrder(TagTable.CardData_CreationTicks)]
        public long CreationTicks;

        [MemoryPackOrder(TagTable.CardData_Tags)]
        public string[] Tags;

        [MemoryPackOrder(TagTable.CardData_Keywords)]
        public string[] Keywords;

        public static SerializableCardData FromCardData(CardData data)
        {
            var dto = new SerializableCardData
            {
                ID = data.ID,
                Supertype = (int)data.Supertype,
                CardName = data.CardName,
                Illustration = data.Illustration,
                Life = data.Life,
                Power = data.Power,
                CreationTicks = data.CreationTime.Ticks,
            };

            // Cost: Dictionary → array
            dto.Cost = data.Cost?.Select(kvp => new CostEntryDTO { ManaType = kvp.Key, Value = kvp.Value }).ToArray()
                       ?? Array.Empty<CostEntryDTO>();

            // Effects: List<EffectData> → array
            dto.Effects = data.Effects?.Select(SerializableEffectData.FromEffectData).ToArray()
                          ?? Array.Empty<SerializableEffectData>();

            dto.Tags = data.Tags?.ToArray() ?? Array.Empty<string>();
            dto.Keywords = data.Keywords?.ToArray() ?? Array.Empty<string>();

            return dto;
        }

        public CardData ToCardData()
        {
            var data = new CardData
            {
                ID = ID,
                Supertype = (Cardtype)Supertype,
                CardName = CardName,
                Illustration = Illustration,
                Life = Life,
                Power = Power,
                CreationTime = new DateTime(CreationTicks),
            };

            // Cost: array → Dictionary
            if (Cost != null)
            {
                data.Cost = new Dictionary<int, float>();
                foreach (var entry in Cost)
                    data.Cost[entry.ManaType] = entry.Value;
            }

            // Effects: array → List<EffectData>
            if (Effects != null)
            {
                data.Effects = Effects.Select(e => e.ToEffectData()).ToList();
            }

            if (Tags != null)
                data.Tags = Tags.ToList();
            if (Keywords != null)
                data.Keywords = Keywords.ToList();

            return data;
        }
    }

    [MemoryPackable]
    public partial class CostEntryDTO
    {
        [MemoryPackOrder(TagTable.CostEntryDTO_ManaType)]
        public int ManaType;

        [MemoryPackOrder(TagTable.CostEntryDTO_Value)]
        public float Value;
    }

    [MemoryPackable]
    public partial class SerializableEffectData
    {
        [MemoryPackOrder(TagTable.EffectData_Abbreviation)]
        public string Abbreviation;

        [MemoryPackOrder(TagTable.EffectData_Initiative)]
        public bool Initiative;

        [MemoryPackOrder(TagTable.EffectData_Parameters)]
        public float Parameters;

        [MemoryPackOrder(TagTable.EffectData_Speed)]
        public int Speed;

        [MemoryPackOrder(TagTable.EffectData_ManaType)]
        public int ManaType;

        [MemoryPackOrder(TagTable.EffectData_Description)]
        public string Description;

        [MemoryPackOrder(TagTable.EffectData_EffectTag)]
        public long EffectTag;

        public void ComputeEffectTag()
        {
            if (!string.IsNullOrEmpty(Description))
                EffectTag = MurmurHash3.EffectTag(Description);
        }

        public static SerializableEffectData FromEffectData(EffectData data)
        {
            var dto = new SerializableEffectData
            {
                Abbreviation = data.Abbreviation,
                Initiative = data.Initiative,
                Parameters = data.Parameters,
                Speed = (int)data.Speed,
                ManaType = (int)data.ManaType,
                Description = data.Description,
            };
            dto.ComputeEffectTag();
            return dto;
        }

        public EffectData ToEffectData()
        {
            return new EffectData
            {
                Abbreviation = Abbreviation,
                Initiative = Initiative,
                Parameters = Parameters,
                Speed = (EffectSpeed)Speed,
                ManaType = (ManaType)ManaType,
                Description = Description,
            };
        }
    }
}
