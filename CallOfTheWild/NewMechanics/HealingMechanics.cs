﻿using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallOfTheWild.HealingMechanics
{

    [AllowedOn(typeof(BlueprintUnitFact))]
    public class IncomingHealingModifier : OwnedGameLogicComponent<UnitDescriptor>, ITargetRulebookSubscriber, IRulebookHandler<RuleHealDamage>
    {
        public ContextValue ModifierPercents;

        public void OnEventAboutToTrigger(RuleHealDamage evt)
        {
            evt.Modifier = new float?((float)this.ModifierPercents.Calculate(this.Fact.MaybeContext) / 100f);
        }

        public void OnEventDidTrigger(RuleHealDamage evt)
        {
        }
    }


    public class UnitPartReceiveBonusCasterLevelHealing : AdditiveUnitPart
    {
        public bool active()
        {
            return !buffs.Empty();
        }
    }


    public class UnitPartSelfHealingMetamagic : AdditiveUnitPart
    {
        public bool hasEmpower(BlueprintAbility spell)
        {
            foreach (var b in buffs)
            {
                var comp = b.Blueprint.GetComponent<SelfHealingMetamagic>();
                if (comp == null)
                {
                    continue;
                }
                if (comp.empower && comp.worksOn(spell))
                {
                    return true;
                }
            }

            return false;
        }

        public bool hasMaximize(BlueprintAbility spell)
        {
            foreach (var b in buffs)
            {
                var comp = b.Blueprint.GetComponent<SelfHealingMetamagic>();
                if (comp == null)
                {
                    continue;
                }
                
                if (comp.maximize && comp.worksOn(spell))
                {
                    return true;
                }
            }

            return false;
        }
    }


    [AllowedOn(typeof(BlueprintUnitFact))]
    public class SelfHealingMetamagic : OwnedGameLogicComponent<UnitDescriptor>, IUnitSubscriber
    {
        public BlueprintAbility[] spells;
        public bool empower = false;
        public bool maximize = false;

        public override void OnTurnOff()
        {
            this.Owner.Ensure<UnitPartSelfHealingMetamagic>().addBuff(this.Fact);
        }


        public override void OnTurnOn()
        {
            this.Owner.Ensure<UnitPartSelfHealingMetamagic>().removeBuff(this.Fact);
        }

        public bool worksOn(BlueprintAbility spell)
        {
            return spells.Contains(spell);
        }
    }





    [AllowedOn(typeof(BlueprintUnitFact))]
    public class ReceiveBonusCasterLevelHealing : OwnedGameLogicComponent<UnitDescriptor>, IUnitSubscriber
    {

        public override void OnTurnOn()
        {
            this.Owner.Ensure<UnitPartReceiveBonusCasterLevelHealing>().addBuff(this.Fact);
        }


        public override void OnTurnOff()
        {
            this.Owner.Ensure<UnitPartReceiveBonusCasterLevelHealing>().removeBuff(this.Fact);
        }
    }


    [Harmony12.HarmonyPatch(typeof(ContextActionHealTarget))]
    [Harmony12.HarmonyPatch("RunAction", Harmony12.MethodType.Normal)]
    class Patch_ContextActionHealTarget_RunAction_Postfix
    {

        static public bool Prefix(ContextActionHealTarget __instance)
        {
            var tr = Harmony12.Traverse.Create(__instance);
            var context = tr.Property("Context").GetValue<MechanicsContext>();
            var target = tr.Property("Target").GetValue<TargetWrapper>().Unit;

            if (target == null)
                UberDebug.LogError(__instance, (object)"Invalid target for effect '{0}'", __instance.GetType().Name);
            else if (context.MaybeCaster == null)
            {
                UberDebug.LogError(__instance, (object)"Caster is missing", (object[])Array.Empty<object>());
            }
            else
            {
                int bonus = __instance.Value.Calculate(context);
                if (!context.HasMetamagic(Metamagic.Maximize) && target.Ensure<UnitPartSelfHealingMetamagic>().hasMaximize(context.SourceAbility) && target == context.MaybeCaster)
                {
                    Main.logger.Log("Maximize Found");
                    bonus = (int)__instance.Value.DiceType * __instance.Value.DiceCountValue.Calculate(context) + __instance.Value.BonusValue.Calculate(context);

                    if (context.HasMetamagic(Metamagic.Empower))
                    {
                        bonus = (int)(bonus * 1.5f);
                    }
                }

                if (!context.HasMetamagic(Metamagic.Empower) && target.Ensure<UnitPartSelfHealingMetamagic>().hasEmpower(context.SourceAbility) && target == context.MaybeCaster)
                {
                    bonus = bonus = (int)(bonus * 1.5f);
                }

                context.TriggerRule<RuleHealDamage>(new RuleHealDamage(context.MaybeCaster, target, DiceFormula.Zero, bonus));

                if (target.Descriptor.Ensure<UnitPartReceiveBonusCasterLevelHealing>().active() && context.SourceAbility != null && __instance.Value != null)
                {
                    int dice_count = __instance.Value.DiceCountValue.Calculate(context);
                    var dice = __instance.Value.DiceType;
                    if (dice_count == 0 || dice == DiceType.Zero)
                    {
                        return false;
                    }
                    int bonus_hp = context.Params.CasterLevel;
                    context.TriggerRule<RuleHealDamage>(new RuleHealDamage(target, target, DiceFormula.Zero, bonus_hp));
                }
            }

            return false;
        }
    }
}