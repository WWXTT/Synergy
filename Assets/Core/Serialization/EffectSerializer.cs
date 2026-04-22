using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;

namespace CardCore.Serialization
{
    public static class EffectSerializer
    {
        public static long ComputeEffectTag(string description)
        {
            if (string.IsNullOrEmpty(description))
                return 0;
            return MurmurHash3.EffectTag(description);
        }

        public static long ComputeEffectTagFromAbbreviation(string abbreviation)
        {
            if (string.IsNullOrEmpty(abbreviation))
                return 0;
            return (long)MurmurHash3.Hash32($"Effect.{abbreviation}", MurmurHash3.SYNERGY_TAG_SEED);
        }

        public static byte[] SerializeEffects(List<EffectData> effects)
        {
            var dtos = effects.Select(e =>
            {
                var dto = SerializableEffectData.FromEffectData(e);
                dto.ComputeEffectTag();
                return dto;
            }).ToArray();
            return MemoryPackSerializer.Serialize(dtos);
        }

        public static List<EffectData> DeserializeEffects(byte[] data)
        {
            var dtos = MemoryPackSerializer.Deserialize<SerializableEffectData[]>(data);
            var result = new List<EffectData>();
            foreach (var dto in dtos)
            {
                // 校验 tag 与描述的一致性
                long expectedTag = ComputeEffectTag(dto.Description);
                if (dto.EffectTag != 0 && expectedTag != 0 && dto.EffectTag != expectedTag)
                    throw new InvalidOperationException(
                        $"Effect tag mismatch for '{dto.Abbreviation}': expected {expectedTag}, got {dto.EffectTag}");
                result.Add(dto.ToEffectData());
            }
            return result;
        }

        public static byte[] SerializeEffectDefinitions(List<EffectDefinition> definitions)
        {
            var dtos = definitions.Select(d =>
            {
                var dto = SerializableEffectDefinition.FromDefinition(d);
                dto.ComputeEffectTag();
                return dto;
            }).ToArray();
            return MemoryPackSerializer.Serialize(dtos);
        }

        public static List<EffectDefinition> DeserializeEffectDefinitions(byte[] data)
        {
            var dtos = MemoryPackSerializer.Deserialize<SerializableEffectDefinition[]>(data);
            return dtos.Select(d => d.ToDefinition()).ToList();
        }
    }
}
