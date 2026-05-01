// 此文件由 Tools/刷新MemoryPackOrder 自动生成，请勿手动修改
// 如需添加新属性，请修改 RefreshMemoryPackOrder.cs 中的 GetTagDefinitions()

namespace CardCore.Serialization
{
    public static class TagTable
    {

        // ---- CardData ----
        public const int CardData_ID = 1411655954;
        public const int CardData_Supertype = 1626364500;
        public const int CardData_CardName = 1077727027;
        public const int CardData_Illustration = 1354286104;
        public const int CardData_Life = 1970882503;
        public const int CardData_Power = 1948219662;
        public const int CardData_Cost = 629661927;
        public const int CardData_Effects = 872057058;
        public const int CardData_CreationTicks = 262055788;
        public const int CardData_Tags = 1046472445;
        public const int CardData_Keywords = 136987752;

        // ---- EffectData ----
        public const int EffectData_Abbreviation = 2081364203;
        public const int EffectData_Initiative = 1768077001;
        public const int EffectData_Parameters = 646202389;
        public const int EffectData_Speed = 822306448;
        public const int EffectData_ManaType = 1186904746;
        public const int EffectData_Description = 32483303;
        public const int EffectData_EffectTag = 142036358;

        // ---- CostEntryDTO ----
        public const int CostEntryDTO_ManaType = 724021260;
        public const int CostEntryDTO_Value = 1598529736;

        // ---- RuntimeCardState ----
        public const int RCS_ID = 244684840;
        public const int RCS_Power = 976050218;
        public const int RCS_Life = 1969295376;
        public const int RCS_MaxLife = 922071213;
        public const int RCS_BaseCost = 1630071019;
        public const int RCS_CostModifier = 1414743304;
        public const int RCS_Armor = 683926072;
        public const int RCS_DamagePrevention = 1221822787;
        public const int RCS_IsTapped = 1985384084;
        public const int RCS_IsFrozen = 1428218013;
        public const int RCS_IsNegated = 2118117958;
        public const int RCS_IsNullified = 364844435;
        public const int RCS_Zone = 204131997;
        public const int RCS_TargetFlags = 1780752744;
        public const int RCS_Keywords = 804741910;
        public const int RCS_Counters = 292357109;

        // ---- CounterEntryDTO ----
        public const int CounterEntryDTO_Key = 524926912;
        public const int CounterEntryDTO_Value = 2046667806;

        // ---- AtomicEffectInstance ----
        public const int AEI_Type = 42652807;
        public const int AEI_Value = 1129914700;
        public const int AEI_Value2 = 408156925;
        public const int AEI_StringValue = 1334908506;
        public const int AEI_ManaTypeParam = 1791664335;
        public const int AEI_ZoneParam = 1641778537;
        public const int AEI_Duration = 1966912189;

        // ---- CostInstance ----
        public const int CI_Type = 57119624;
        public const int CI_Value = 832738714;
        public const int CI_ManaType = 1850397482;

        // ---- ActivationCondition ----
        public const int AC_Type = 1310782459;
        public const int AC_Value = 1634179753;
        public const int AC_Value2 = 21033891;

        // ---- EffectDefinition ----
        public const int ED_Id = 548492777;
        public const int ED_DisplayName = 1300219100;
        public const int ED_Description = 1631759857;
        public const int ED_BaseSpeed = 1508539253;
        public const int ED_ActivationType = 306060205;
        public const int ED_TriggerTiming = 984178182;
        public const int ED_IsOptional = 640126085;
        public const int ED_Duration = 1013231263;
        public const int ED_Effects = 973707135;
        public const int ED_Costs = 531244005;
        public const int ED_Tags = 79464002;
        public const int ED_SourceCardId = 863828489;
        public const int ED_TargetType = 953648607;
        public const int ED_EffectTag = 550171593;
        public const int ED_ActivationConditions = 685342510;
        public const int ED_TriggerConditions = 1516982108;

        // ---- NetworkMessage ----
        public const int NM_MessageType = 790136053;
        public const int NM_SequenceId = 885822028;
        public const int NM_Payload = 1388875668;
        public const int NM_Timestamp = 1097508369;

        // ---- MsgPlayCard ----
        public const int MPC_CardID = 1094377983;
        public const int MPC_FromZone = 2059120564;
        public const int MPC_ToZone = 2126325883;
        public const int MPC_ChosenManaType = 1841415904;

        // ---- MsgActivateEffect ----
        public const int MAE_SourceCardID = 784640788;
        public const int MAE_EffectTag = 926061686;
        public const int MAE_ActivationSpeed = 1788861467;
        public const int MAE_TargetIDs = 502958227;

        // ---- MsgGameStateSync ----
        public const int MGSS_CurrentTurn = 2035353205;
        public const int MGSS_CurrentPhase = 1337617274;
        public const int MGSS_Players = 1169411298;
        public const int MGSS_BattlefieldCards = 574488751;
        public const int MGSS_Stack = 272663845;

        // ---- PlayerState ----
        public const int PS_Name = 1780016741;
        public const int PS_Life = 1638819233;
        public const int PS_MaxHealth = 459554504;
        public const int PS_DeckCount = 1940827978;
        public const int PS_HandCount = 38995153;

        // 共 89 个标签
    }
}
