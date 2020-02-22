﻿using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Controllers.Brain.Blueprints;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using Kingmaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace CallOfTheWild
{
    partial class MysteryEngine
    {
        public BlueprintFeature createBurningMagic(string name_prefix, string display_name, string description)
        {
            var burn = library.CopyAndAdd<BlueprintBuff>("e92ecaa76b5db674fa5b0aaff5b21bc9", name_prefix + "Buff", "");
            burn.SetDescription("This creature is burning and takes 1 point of fire damage per spell level each turn.");

            var burn_damage = Helpers.CreateActionDealDamage(DamageEnergyType.Fire,
                                                             Helpers.CreateContextDiceValue(DiceType.Zero, 0, Helpers.CreateContextValue(AbilityRankType.Default)));
            burn.ReplaceComponent<AddFactContextActions>(a => a.NewRound = Helpers.CreateActionList(burn_damage, a.NewRound.Actions[1]));

            burn.AddComponent(Helpers.CreateContextRankConfig(ContextRankBaseValueType.CustomProperty, ContextRankProgression.AsIs,
                                                              customProperty: NewMechanics.SpellLevelPropertyGetter.Blueprint.Value));
            var on_dmg_action = Helpers.CreateActionList(Common.createContextActionApplyBuff(burn, Helpers.CreateContextDuration(0, DurationRate.Rounds, DiceType.D4, 1), is_from_spell: true));
            var feature = Helpers.CreateFeature(name_prefix + "Feature",
                                            display_name,
                                            description,
                                            "",
                                            burn.Icon,
                                            FeatureGroup.None,
                                            Helpers.Create<NewMechanics.ActionOnSpellDamage>(a =>
                                                                                            {
                                                                                                a.descriptor = SpellDescriptor.Fire;
                                                                                                a.use_existing_save = true;
                                                                                                a.action_only_on_save = true;
                                                                                                a.action = on_dmg_action;
                                                                                            }
                                                                                            )
                                            );
            return feature;
        }


        public BlueprintFeature createCinderDance(string name_prefix, string display_name, string description)
        {
            var icon = LoadIcons.Image2Sprite.Create(@"AbilityIcons/CinderDance.png");
            var woodland_stride = library.CopyAndAdd<BlueprintFeature>("11f4072ea766a5840a46e6660894527d", name_prefix + "2Feature", "");
            woodland_stride.HideInCharacterSheetAndLevelUp = true;
            woodland_stride.SetNameDescriptionIcon("", "", null);

            var feat = Helpers.CreateFeature(name_prefix + "Feature",
                                                 display_name,
                                                 description,
                                                 "",
                                                 icon,
                                                 FeatureGroup.None,
                                                 Helpers.CreateAddStatBonus(StatType.Speed, 10, ModifierDescriptor.UntypedStackable),
                                                 Helpers.CreateAddFeatureOnClassLevel(woodland_stride, 10, classes)
                                                 );
            return feat;
        }


        public BlueprintFeature createFireBreath(string name_prefix, string display_name, string description)
        {
            var resource = Helpers.CreateAbilityResource(name_prefix + "Resource", "", "", "", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 5, 1, 5, 1, 0, 0.0f, classes);

            var ability = library.CopyAndAdd<BlueprintAbility>("4783c3709a74a794dbe7c8e7e0b1b038", name_prefix + "Ability", "");
            ability.Type = AbilityType.Supernatural;
            ability.SpellResistance = false;
            ability.RemoveComponents<SpellComponent>();
            ability.RemoveComponents<SpellListComponent>();
            ability.ReplaceComponent<ContextRankConfig>(Helpers.CreateContextRankConfig(baseValueType: ContextRankBaseValueType.ClassLevel, classes: classes));
            ability.AddComponents(Helpers.CreateResourceLogic(resource),
                                  Common.createContextCalculateAbilityParamsBasedOnClasses(classes, stat)
                                 );
            ability.SetNameDescriptionIcon(display_name, description, Helpers.GetIcon("2a711cd134b91d34ab027b50d721778b")); // gold dragon fire breath)

            var feature = Common.AbilityToFeature(ability, false);
            feature.AddComponent(Helpers.CreateAddAbilityResource(resource));
            return feature;
        }


        public BlueprintFeature CreateFirestorm(string name_prefix, string display_name, string description)
        {
            // This is a cross between Fire Storm and Incendiary Cloud.
            // Like Fire Storm, it does 1d6 per caster level.
            // Like Incendiary Cloud, it's a persistent AOE (shorter duration though).

            var name = name_prefix + "";
            var area = library.CopyAndAdd<BlueprintAbilityAreaEffect>(
                            "35a62ad81dd5ae3478956c61d6cd2d2e", // IncendiaryCloudArea, used by brass golem breath
                            $"{name}Area", "");

            // TODO: offer the option to place more, using an activatable ability?
            area.Size = 20.Feet();
            area.SetComponents(
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, ContextRankProgression.AsIs, AbilityRankType.DamageDice, classes: classes),
                Helpers.CreateAreaEffectRunAction(round:
                    Helpers.CreateActionSavingThrow(SavingThrowType.Reflex,
                        Helpers.CreateActionDealDamage(DamageEnergyType.Fire,
                            DiceType.D6.CreateContextDiceValue(AbilityRankType.DamageDice.CreateContextValue()),
                            halfIfSaved: true))));

            var resource = Helpers.CreateAbilityResource($"{name}Resource", "", "", "", null);
            resource.SetFixedResource(1);

            var spawn_area = Common.createContextActionSpawnAreaEffect(area, Helpers.CreateContextDuration(Helpers.CreateContextValue(AbilityRankType.Default)));
            var ability = Helpers.CreateAbility($"{name}Ability",
                                                display_name,
                                                description,
                                                "",
                                                NewSpells.incendiary_cloud.Icon,
                                                AbilityType.Supernatural,
                                                CommandType.Standard,
                                                AbilityRange.Close,
                                                $"1 round/{stat.ToString()} modifier",
                                                Helpers.reflexHalfDamage,
                                                resource.CreateResourceLogic(),
                                                Common.createAbilityAoERadius(area.Size, Kingmaker.UnitLogic.Abilities.Components.TargetType.Any),
                                                Helpers.CreateSpellDescriptor(SpellDescriptor.Fire),
                                                Helpers.CreateContextRankConfig(ContextRankBaseValueType.StatBonus, ContextRankProgression.AsIs, stat: stat),
                                                Helpers.CreateRunActions(spawn_area),
                                                Common.createContextCalculateAbilityParamsBasedOnClasses(classes, stat)
                                                );

            ability.setMiscAbilityParametersRangedDirectional();

            var feature = Common.AbilityToFeature(ability, false);
            feature.AddComponent(Helpers.CreateAddAbilityResource(resource));
            foreach (var c in classes)
            {
                feature.AddComponents(Helpers.PrerequisiteClassLevel(c, 11, any: true));
            }
            return feature;
        }

        public BlueprintFeature createFormOfFlame(string name_prefix, string display_name, string description)
        {
            var resource = Helpers.CreateAbilityResource($"{name_prefix}Resource", "", "", "", null);
            resource.SetFixedResource(1);

            var form_ids = new string[] {
                "bb6bb6d6d4b27514dae8ec694433dcd3",
                "9a87d2fb0e288664c8dff299ff030a38",
                "2c40b391368f05e4b91aa8a8a51109ef",
                "c281eeecc554b72449fef43924e522ce"
            };

            var feature = Helpers.CreateFeature(name_prefix + "Feature",
                                                display_name,
                                                description,
                                                "",
                                                Helpers.GetIcon(form_ids[0]),
                                                FeatureGroup.None,
                                                Helpers.CreateAddAbilityResource(resource)
                                                );


            for (int i = 0; i < form_ids.Length; i++)
            {
                var spell = library.Get<BlueprintAbility>(form_ids[i]);
                var ability = Common.convertToSuperNatural(spell, name_prefix, classes, stat, resource);
                
                var new_actions = Common.changeAction<ContextActionApplyBuff>(ability.GetComponent<AbilityEffectRunAction>().Actions.Actions, c => c.DurationValue = Helpers.CreateContextDuration(c.DurationValue.BonusValue, DurationRate.Hours));
                ability.ReplaceComponent<AbilityEffectRunAction>(a => a.Actions = Helpers.CreateActionList(new_actions));
               
                if (i == 0)
                {
                    feature.AddComponent(Helpers.CreateAddFact(ability));
                }
                else
                {
                    var feat = Common.AbilityToFeature(ability);
                    feature.AddComponent(Helpers.CreateAddFeatureOnClassLevel(feat, 7 + i*2, classes));
                }

            }
            foreach (var c in classes)
            {
                feature.AddComponents(Helpers.PrerequisiteClassLevel(c, 7, any: true));
            }
            return feature;
        }


        public BlueprintFeature createHeatAura(string name_prefix, string display_name, string description)
        {
            var resource = Helpers.CreateAbilityResource($"{name_prefix}Resource", "", "", "", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 5, 1, 5, 1, 0, 0, classes);

            var blur = library.Get<BlueprintBuff>("dd3ad347240624d46a11a092b4dd4674");

            var damage = Helpers.CreateConditional(Helpers.Create<ContextConditionIsCaster>(),
                                                   null,
                                                   Helpers.CreateActionSavingThrow(SavingThrowType.Reflex,
                                                                                   Helpers.CreateActionDealDamage(DamageEnergyType.Fire, DiceType.D4.CreateContextDiceValue(), isAoE: true, halfIfSaved: true))
                                                  );

            var apply_blur = Helpers.CreateConditional(Helpers.Create<ContextConditionIsCaster>(), Common.createContextActionApplyBuff(blur, Helpers.CreateContextDuration(1), dispellable: false));
            var ability = Helpers.CreateAbility($"{name_prefix}Ability",
                                                display_name,
                                                description,
                                                "",
                                                NewSpells.wall_of_fire.Icon,
                                                AbilityType.Supernatural,
                                                CommandType.Swift,
                                                AbilityRange.Personal,
                                                Helpers.oneRoundDuration,
                                                Helpers.reflexHalfDamage,
                                                Helpers.CreateRunActions(damage, apply_blur),
                                                Helpers.CreateAbilityTargetsAround(10.Feet(), Kingmaker.UnitLogic.Abilities.Components.TargetType.Any),
                                                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, ContextRankProgression.Div2,
                                                                                min: 1, classes: classes),
                                                resource.CreateResourceLogic(),
                                                Helpers.CreateSpellDescriptor(SpellDescriptor.Fire),
                                                Common.createContextCalculateAbilityParamsBasedOnClasses(classes, stat));
            ability.setMiscAbilityParametersSelfOnly();

            var feature = Common.AbilityToFeature(ability, false);
            feature.AddComponent(Helpers.CreateAddAbilityResource(resource));
            return feature;
        }


        public BlueprintFeature createMoltenSkin(string name_prefix, string display_name, string description)
        {
            var icon = Helpers.GetIcon("ddfb4ac970225f34dbff98a10a4a8844");

            var feature = Helpers.CreateFeature(name_prefix + "Feature",
                                                   display_name,
                                                   description,
                                                   "",
                                                   icon,
                                                   FeatureGroup.None,
                                                   Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel,
                                                                                  ContextRankProgression.Custom,
                                                                                  classes: classes,
                                                                                  customProgression: new (int, int)[] {
                                                                                                                        (4, 5),
                                                                                                                        (10, 10),
                                                                                                                        (20, 20)
                                                                                                                      }),
                                                   Helpers.Create<AddDamageResistanceEnergy>(a =>
                                                                                            {
                                                                                                a.Type = DamageEnergyType.Fire;
                                                                                                a.Value = Helpers.CreateContextValueRank();
                                                                                            }
                                                                                            )
                                                   );

            var immunity = Helpers.CreateFeature(name_prefix + "ImmunityFeature",
                                                     display_name,
                                                     description,
                                                     "",
                                                     icon,
                                                     FeatureGroup.None,
                                                     Helpers.Create<AddEnergyDamageImmunity>(a => a.EnergyType = DamageEnergyType.Fire)
                                                     );

            feature.AddComponent(Helpers.CreateAddFeatureOnClassLevel(immunity, 17, classes));
            return feature;
        }


        public BlueprintFeature createTouchOfFlame(string name_prefix, string display_name, string description)
        {
            var icon = library.Get<BlueprintAbility>("4783c3709a74a794dbe7c8e7e0b1b038").Icon; //burning hands
            var resource = Helpers.CreateAbilityResource(name_prefix + "Resource", "", "", "", null);
            resource.SetIncreasedByStat(3, stat);
            var touch_of_flames = library.CopyAndAdd<BlueprintAbility>("4ecdf240d81533f47a5279f5075296b9", name_prefix + "Ability", ""); //fire domain fire bolt 
            touch_of_flames.AddComponent(Helpers.CreateSpellDescriptor(SpellDescriptor.Fire));
            touch_of_flames.RemoveComponents<SpellComponent>();
            touch_of_flames.RemoveComponents<AbilityResourceLogic>();
            touch_of_flames.ReplaceComponent<AbilityDeliverProjectile>(Helpers.CreateDeliverTouch());
            touch_of_flames.setMiscAbilityParametersTouchHarmful();
            touch_of_flames.Range = AbilityRange.Touch;
            touch_of_flames.Type = AbilityType.Supernatural;
            touch_of_flames.SpellResistance = false;
            touch_of_flames.SetNameDescriptionIcon(display_name,
                                                   description,
                                                   icon);
            touch_of_flames.ReplaceComponent<ContextRankConfig>(c => Helpers.SetField(c, "m_Class", classes));
            var touch_of_flames_sticky = Helpers.CreateTouchSpellCast(touch_of_flames, resource);
            var flaming = library.Get<BlueprintWeaponEnchantment>("30f90becaaac51f41bf56641966c4121");

            var flaming_weapon_feature = Helpers.CreateFeature(name_prefix + "FlamingWeaponFeature",
                                                          "",
                                                          "",
                                                          "",
                                                          null,
                                                          FeatureGroup.None,
                                                          Helpers.Create<NewMechanics.EnchantmentMechanics.PersistentWeaponEnchantment>(p => p.enchant = flaming)
                                                          );

            flaming_weapon_feature.HideInCharacterSheetAndLevelUp = true;

            var feature = Helpers.CreateFeature(name_prefix + "Feature",
                                                   display_name,
                                                   description,
                                                   "",
                                                   touch_of_flames.Icon,
                                                   FeatureGroup.None,
                                                   Helpers.CreateAddFact(touch_of_flames_sticky),
                                                   Helpers.CreateAddAbilityResource(resource),
                                                   Helpers.CreateAddFeatureOnClassLevel(flaming_weapon_feature, 11, classes)
                                                   );
            return feature;
        }

    }
}