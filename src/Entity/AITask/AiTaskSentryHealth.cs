using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using System;

namespace VSKingdom {
	public class AiTaskSentryHealth : AiTaskBase {
		public AiTaskSentryHealth(EntityAgent entity) : base(entity) { }

		protected bool animStarted;
		protected float healingFactor;
		protected EntityBehaviorHealth healthBehavior;

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			animMeta = new AnimationMetaData() {
				Code = "BandageSelf".ToLowerInvariant(),
				Animation = "BandageSelf".ToLowerInvariant(),
				AnimationSpeed = 1f
			}.Init();
		}

		public override void AfterInitialize() {
			base.AfterInitialize();
			healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
		}

		public override bool ShouldExecute() {
			// Determine if the entity is injured, and if so, how badly.
			if (healthBehavior.Health < healthBehavior.MaxHealth) {
				if (entity is EntitySentry thisEnt && !thisEnt.HealItemSlot.Empty && thisEnt.HealItemSlot.Itemstack.Collectible.Attributes["health"].Exists) {
					if ((healthBehavior.Health / healthBehavior.MaxHealth) < 0.75) {
						return true;
					}
				}
			}
			// Healing is for wussies!
			return false;
		}

		public override void StartExecute() {
			// TODO: Make executable command to go find cover before healing! Possibly take off armor or assess healing factor.
			healingFactor = entity.GearInventory[19].Itemstack.Collectible.Attributes["health"].AsFloat();
			animStarted = false;
		}

		public override bool ContinueExecute(float dt) {
			// Start the animation if not done so already and play a little sound effect.
			if (animMeta != null && !animStarted) {
				animStarted = true;
				animMeta.EaseInSpeed = 1f;
				animMeta.EaseOutSpeed = 1f;
				entity.AnimManager.StartAnimation(animMeta);
				entity.World.PlaySoundAt(new AssetLocation("game:sounds/player/poultice"), entity, null, false);
			}
			if (animStarted && !entity.AnimManager.IsAnimationActive(animMeta.ToString())) {
				return false;
			}
			return true;
		}

		public override void FinishExecute(bool cancelled) {
			// Only do if the execution finished properly.
			if (!cancelled) {
				// Apply health as "damage" to the entity.
				entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = healingFactor > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healingFactor));
				// If infiniteHealing items are disabled, remove it from their inventory slot.
				if (!entity.Api.World.Config.GetAsBool("InfiniteHeal")) {
					entity.GearInventory[19].TakeOut(1);
					entity.GearInventory[19].MarkDirty();
				}
			}
		}
	}
}