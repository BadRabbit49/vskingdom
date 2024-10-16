using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class KingdomPatches {

		public static void Patch(Harmony harmony) {
			harmony.Patch(methodInfo(), prefix: new HarmonyMethod(typeof(KingdomPatches).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public)));
		}

		public static void Unpatch(Harmony harmony) {
			harmony.Unpatch(methodInfo(), HarmonyPatchType.Prefix, "badrabbit49.vskingdom");
		}

		public static MethodInfo methodInfo() {
			return typeof(ItemPoultice).GetMethod("OnHeldInteractStop", BindingFlags.Instance | BindingFlags.Public);
		}

		public static bool Prefix(float secondsUsed, ItemSlot slot, EntityAgent byEntity, EntitySelection entitySel) {
			if (!entitySel?.Entity?.HasBehavior<EntityBehaviorDecayBody>() ?? !entitySel?.Entity?.Properties?.Attributes?["canRevive"]?.AsBool(false) ?? true) {
				return true;
			}
			if (secondsUsed > 0.7f && byEntity.World.Side == EnumAppSide.Server) {
				if (entitySel.Entity.Alive) {
					JsonObject attr = slot.Itemstack.Collectible.Attributes;
					float health = attr["health"].AsFloat();
					entitySel.Entity.ReceiveDamage(new DamageSource() {
						Source = EnumDamageSource.Internal,
						Type = health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison,
						CauseEntity = byEntity,
						SourceEntity = byEntity
					}, Math.Abs(health));
					slot.TakeOut(1);
					slot.MarkDirty();
				} else {
					EnumHandling handled = EnumHandling.PassThrough;
					entitySel.Entity.GetBehavior<EntityBehaviorDecayBody>()?.OnInteract(byEntity, slot, entitySel.HitPosition, EnumInteractMode.Interact, ref handled);
					if (entitySel.Entity.Properties.Attributes["canRevive"].AsBool(false)) {
						entitySel.Entity.Revive();
						slot.TakeOut(1);
						slot.MarkDirty();
					}
				}
			}
			return false;
		}
	}
}
