using System.Collections.Generic;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using static Vintagestory.API.Common.EntityAgent;

namespace VSKingdom {
	internal static class HealthUtility {
		public static float handleDamaged(ICoreAPI api, EntityHumanoid ent, float dmg, DamageSource src) {
			EnumDamageType type = src.Type;
			// Reduce damage if ent holds a shield
			dmg = applyShieldProtection(api, ent, dmg, src);
			// No reason to do all this if the damage was less than 0.
			if (dmg <= 0) {
				return 0;
			}
			// The code below only the server needs to execute.
			if (api.Side == EnumAppSide.Client) {
				return dmg;
			}
			// Does not protect against non-attack damages.
			if (type != EnumDamageType.BluntAttack && type != EnumDamageType.PiercingAttack && type != EnumDamageType.SlashingAttack) {
				return dmg;
			}
			// Does not protect against stuff like hunger or poisoning.
			if (src.Source == EnumDamageSource.Internal || src.Source == EnumDamageSource.Suicide) {
				return dmg;
			}

			ItemSlot armorSlot;
			IInventory inv = ent.GearInventory;
			double rnd = api.World.Rand.NextDouble();
			int attackTarget;

			// Apply different attackTargets depending on the rnd value.
			if ((rnd -= 0.2) < 0) {
				// Head Armor.
				armorSlot = inv[12];
				attackTarget = 0;
			} else if ((rnd -= 0.5) < 0) {
				// Body Armor.
				armorSlot = inv[13];
				attackTarget = 1;
			} else {
				// Legs Armor.
				armorSlot = inv[14];
				attackTarget = 2;
			}

			// Dictionary for all the targetable clothing for attacks.
			Dictionary<int, EnumCharacterDressType[]> clothingDamageTargetsByAttackTacket = new Dictionary<int, EnumCharacterDressType[]>() {
				{ 0, new EnumCharacterDressType[] { EnumCharacterDressType.Head, EnumCharacterDressType.Face, EnumCharacterDressType.Neck } },
				{ 1, new EnumCharacterDressType[] { EnumCharacterDressType.UpperBody, EnumCharacterDressType.UpperBodyOver, EnumCharacterDressType.Shoulder, EnumCharacterDressType.Arm, EnumCharacterDressType.Hand } },
				{ 2, new EnumCharacterDressType[] { EnumCharacterDressType.LowerBody, EnumCharacterDressType.Foot } }
			};

			// Apply full damage if no armor is in this slot.
			if (armorSlot.Empty || !(armorSlot.Itemstack.Item is ItemWearable) || armorSlot.Itemstack.Collectible.GetRemainingDurability(armorSlot.Itemstack) <= 0) {
				EnumCharacterDressType[] dressTargets = clothingDamageTargetsByAttackTacket[attackTarget];
				EnumCharacterDressType target = dressTargets[api.World.Rand.Next(dressTargets.Length)];
				ItemSlot targetslot = ent.GearInventory[(int)target];
				// Apply damage depending on type of attack, if the targetslot isn't empty of course.
				if (!targetslot.Empty) {
					float mul = 0.25f;
					if (type == EnumDamageType.SlashingAttack) {
						mul = 1f;
					}
					if (type == EnumDamageType.PiercingAttack) {
						mul = 0.5f;
					}
					float diff = -dmg / 100 * mul;
					(targetslot.Itemstack.Collectible as ItemWearable)?.ChangeCondition(targetslot, diff);
				}
				return dmg;
			}

			ProtectionModifiers protMods = (armorSlot.Itemstack.Item as ItemWearable).ProtectionModifiers;
			int weaponTier = src.DamageTier;
			float flatDmgProt = protMods.FlatDamageReduction;
			float percentProt = protMods.RelativeProtection;
			for (int tier = 1; tier <= weaponTier; tier++) {
				bool aboveTier = tier > protMods.ProtectionTier;
				float flatLoss = aboveTier ? protMods.PerTierFlatDamageReductionLoss[1] : protMods.PerTierFlatDamageReductionLoss[0];
				float percLoss = aboveTier ? protMods.PerTierRelativeProtectionLoss[1] : protMods.PerTierRelativeProtectionLoss[0];
				// Determine if to apply a flat and protective loss.
				if (aboveTier && protMods.HighDamageTierResistant) {
					flatLoss /= 2;
					percLoss /= 2;
				}
				flatDmgProt -= flatLoss;
				percentProt *= 1 - percLoss;
			}
			// Durability loss is the one before the damage reductions.
			float durabilityLoss = 0.5f + dmg * Math.Max(0.5f, (weaponTier - protMods.ProtectionTier) * 3);
			int durabilityLossInt = GameMath.RoundRandom(api.World.Rand, durabilityLoss);
			// Now reduce the damage.
			dmg = Math.Max(0, dmg - flatDmgProt);
			dmg *= 1 - Math.Max(0, percentProt);
			armorSlot.Itemstack.Collectible.DamageItem(api.World, ent, armorSlot, durabilityLossInt);
			// If the armorSlot is now empty from breaking, play a sound effect.
			if (armorSlot.Empty) {
				api.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), ent);
			}
			return dmg;
		}

		public static float applyShieldProtection(ICoreAPI api, EntityHumanoid ent, float damage, DamageSource dmgSource) {
			double horizontalAngleProtectionRange = 120 / 2 * GameMath.DEG2RAD;
			ItemSlot[] shieldSlots = new ItemSlot[] { ent.LeftHandItemSlot, ent.RightHandItemSlot };
			for (int i = 0; i < shieldSlots.Length; i++) {
				var shieldSlot = shieldSlots[i];
				var attr = shieldSlot.Itemstack?.ItemAttributes?["shield"];
				if (attr is null || !attr.Exists) {
					continue;
				}
				string usetype = ent.Controls.Sneak ? "active" : "passive";
				float dmgabsorb = attr["damageAbsorption"][usetype].AsFloat(0);
				float chance = attr["protectionChance"][usetype].AsFloat(0);
				double dx;
				double dy;
				double dz;
				if (dmgSource.HitPosition != null) {
					dx = dmgSource.HitPosition.X;
					dy = dmgSource.HitPosition.Y;
					dz = dmgSource.HitPosition.Z;
				} else if (dmgSource.SourceEntity != null) {
					dx = dmgSource.SourceEntity.Pos.X - ent.Pos.X;
					dy = dmgSource.SourceEntity.Pos.Y - ent.Pos.Y;
					dz = dmgSource.SourceEntity.Pos.Z - ent.Pos.Z;
				} else if (dmgSource.SourcePos != null) {
					dx = dmgSource.SourcePos.X - ent.Pos.X;
					dy = dmgSource.SourcePos.Y - ent.Pos.Y;
					dz = dmgSource.SourcePos.Z - ent.Pos.Z;
				} else {
					break;
				}
				double entYaw = ent.Pos.Yaw + GameMath.PIHALF;
				double entPitch = ent.Pos.Pitch;
				double attackYaw = Math.Atan2((double)dx, (double)dz);
				double a = dy;
				float b = (float)Math.Sqrt(dx * dx + dz * dz);
				float attackPitch = (float)Math.Atan2(a, b);
				bool verticalAttack = Math.Abs(attackPitch) > 65 * GameMath.DEG2RAD;
				bool inProtectionRange;
				// Apply specific inProtectionRange depending on if the attack was a vertical one or not.
				if (verticalAttack) {
					inProtectionRange = Math.Abs(GameMath.AngleRadDistance((float)entPitch, (float)attackPitch)) < 30 * GameMath.DEG2RAD;
				} else {
					inProtectionRange = Math.Abs(GameMath.AngleRadDistance((float)entYaw, (float)attackYaw)) < horizontalAngleProtectionRange;
				}
				if (inProtectionRange && api.World.Rand.NextDouble() < chance) {
					damage = Math.Max(0, damage - dmgabsorb);
					var loc = shieldSlot.Itemstack.ItemAttributes["blockSound"].AsString("held/shieldblock");
					api.World.PlaySoundAt(AssetLocation.Create(loc, shieldSlot.Itemstack.Collectible.Code.Domain).WithPathPrefixOnce("sounds/").WithPathAppendixOnce(".ogg"), ent, null);
					(api as ICoreServerAPI).Network.BroadcastEntityPacket(ent.EntityId, (int)EntityServerPacketId.PlayPlayerAnim, SerializerUtil.Serialize("shieldBlock" + ((i == 0) ? "L" : "R")));
					if (api.Side == EnumAppSide.Server) {
						shieldSlot.Itemstack.Collectible.DamageItem(api.World, dmgSource.SourceEntity, shieldSlot, 1);
						shieldSlot.MarkDirty();
					}
				}
			}
			return damage;
		}
	}
}