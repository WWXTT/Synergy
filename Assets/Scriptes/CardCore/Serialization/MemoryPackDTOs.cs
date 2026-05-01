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
        public SerializableCardEffectData[] Effects;

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

            dto.Cost = data.Cost?.Select(kvp => new CostEntryDTO { ManaType = kvp.Key, Value = kvp.Value }).ToArray()
                       ?? Array.Empty<CostEntryDTO>();

            dto.Effects = data.Effects?.Select(SerializableCardEffectData.FromCardEffectData).ToArray()
                          ?? Array.Empty<SerializableCardEffectData>();

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

            if (Cost != null)
            {
                data.Cost = new Dictionary<int, float>();
                foreach (var entry in Cost)
                    data.Cost[entry.ManaType] = entry.Value;
            }

            if (Effects != null)
            {
                data.Effects = Effects.Select(e => e.ToCardEffectData()).ToList();
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
    public partial class SerializableCardEffectData
    {
        [MemoryPackOrder(TagTable.EffectData_Abbreviation)]
        public string Id;

        [MemoryPackOrder(TagTable.EffectData_Description)]
        public string Description;

        [MemoryPackOrder(TagTable.EffectData_Initiative)]
        public int TriggerTiming;

        [MemoryPackOrder(TagTable.EffectData_Speed)]
        public int ActivationType;

        [MemoryPackOrder(TagTable.EffectData_Parameters)]
        public int BaseSpeed;

        [MemoryPackOrder(TagTable.EffectData_ManaType)]
        public SerializableAtomicEffectEntry[] AtomicEffects;

        public static SerializableCardEffectData FromCardEffectData(CardEffectData data)
        {
            var dto = new SerializableCardEffectData
            {
                Id = data.Id,
                Description = data.Description,
                TriggerTiming = data.TriggerTiming,
                ActivationType = data.ActivationType,
                BaseSpeed = data.BaseSpeed,
            };
            dto.AtomicEffects = data.AtomicEffects?.Select(SerializableAtomicEffectEntry.FromEntry).ToArray()
                                ?? Array.Empty<SerializableAtomicEffectEntry>();
            return dto;
        }

        public CardEffectData ToCardEffectData()
        {
            return new CardEffectData
            {
                Id = Id,
                Description = Description,
                TriggerTiming = TriggerTiming,
                ActivationType = ActivationType,
                BaseSpeed = BaseSpeed,
                AtomicEffects = AtomicEffects?.Select(e => e.ToEntry()).ToList() ?? new List<AtomicEffectEntry>(),
            };
        }
    }

    [MemoryPackable]
    public partial class SerializableAtomicEffectEntry
    {
        [MemoryPackOrder(TagTable.AEI_Type)]
        public string EffectType;

        [MemoryPackOrder(TagTable.AEI_Value)]
        public int Value;

        [MemoryPackOrder(TagTable.AEI_Value2)]
        public int Value2;

        [MemoryPackOrder(TagTable.AEI_StringValue)]
        public string StringValue;

        [MemoryPackOrder(TagTable.AEI_ManaTypeParam)]
        public int ManaTypeParam;

        [MemoryPackOrder(TagTable.AEI_ZoneParam)]
        public int ZoneParam;

        [MemoryPackOrder(TagTable.AEI_Duration)]
        public int Duration;

        public static SerializableAtomicEffectEntry FromEntry(AtomicEffectEntry entry)
        {
            return new SerializableAtomicEffectEntry
            {
                EffectType = entry.EffectType,
                Value = entry.Value,
                Value2 = entry.Value2,
                StringValue = entry.StringValue,
                ManaTypeParam = entry.ManaTypeParam,
                ZoneParam = entry.ZoneParam,
                Duration = entry.Duration,
            };
        }

        public AtomicEffectEntry ToEntry()
        {
            return new AtomicEffectEntry
            {
                EffectType = EffectType,
                Value = Value,
                Value2 = Value2,
                StringValue = StringValue,
                ManaTypeParam = ManaTypeParam,
                ZoneParam = ZoneParam,
                Duration = Duration,
            };
        }
    }
}
