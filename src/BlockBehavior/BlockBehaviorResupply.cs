using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using System.Collections.Generic;
using Vintagestory.API.Client;
using System;

namespace VSKingdom {
	public class BlockBehaviorResupply : BlockEntityBehavior {
		bool fillAmmo;
		int ammoAmnt;
		int healRate;
		int healAmnt;
		public string Type => "guardPost";
		public Vec3d Position => Blockentity.Pos.ToVec3d();
		public BlockEntity be;
		public BlockEntityPost bePost;
		public BlockBehaviorResupply(BlockEntity blockentity) : base(blockentity) {
			be = blockentity;
			bePost = blockentity as BlockEntityPost;
		}
		protected ILoadedSound ambientSound;
		public override void Initialize(ICoreAPI api, JsonObject properties) {
			base.Initialize(api, properties);
			// Should ammo be refilled for soldiers?
			fillAmmo = bePost.metlTier >= 4;
			// Ammunition regeneration value every tick.
			ammoAmnt = bePost.metlTier;
			// Health regeneration rate in seconds.
			healRate = bePost.metlTier * 60000;
			// Health regeneration value every tick.
			healAmnt = bePost.metlTier;
			/** TODO: SETUP LISTENER TO PASSIVELY REGEN HEALTH AND RESPAWN TOKENS OVER TIME!!! **/
			if (Blockentity.Api.Side == EnumAppSide.Client && bePost != null) {
				Blockentity.RegisterGameTickListener(OnTickOffset, healRate);
			}
		}

		private bool IsNight {
			get {
				float str = Api.World.Calendar.GetDayLightStrength(Blockentity.Pos.X, Blockentity.Pos.Z);
				return str < 0.4;
			}
		}

		private bool HasNearbySoldiers {
			get {
				for (int s = 0; s < bePost.capacity; s++) {
					if (Api.World.GetEntityById(bePost.soldierIds[s]).Pos.DistanceTo(Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5)) < bePost.areasize) {
						return true;
					}
				}
				return false;
			}
		}

		private void OnTickOffset(float dt) {
			if (!bePost.fireLive) {
				return;
			}
			// Try to heal, revive, and resupply all soldiers in the list if in nearby range.
			if (HasNearbySoldiers && bePost.soldierIds.Count != 0) {
				List<long> soldierList = bePost.soldierIds;
				foreach (long soldierID in soldierList) {
					EntityArcher soldier = Api.World.GetEntityById(soldierID) as EntityArcher;
					if (soldier.Pos.DistanceTo(Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5)) <= bePost.areasize) {
						// Heal soldier by given amount.
						soldier.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = healAmnt > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healAmnt));
						// Refill ammo by given amount if enabled.
						if (fillAmmo && Api.World.Config.GetAsBool("AllowResupply") && !Api.World.Config.GetAsBool("InfiniteAmmos")) {
							if (soldier?.AmmoItemSlot?.Itemstack?.StackSize < soldier?.AmmoItemSlot?.Itemstack?.Collectible?.MaxStackSize) {
								soldier.AmmoItemSlot.Itemstack.StackSize += ammoAmnt;
							}
						}
					}
				}
			}
		}

		#region Sound
		public virtual float SoundLevel {
			get {
				return 0.66f;
			}
		}

		public void ToggleAmbientSounds(bool on) {
			if (Api.Side != EnumAppSide.Client) {
				return;
			}
			if (on) {
				if (ambientSound is null || !ambientSound.IsPlaying) {
					ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams() {
						Location = new AssetLocation("game:sounds/environment/fireplace"),
						ShouldLoop = true,
						Position = be.Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
						DisposeOnFinish = false,
						Volume = SoundLevel
					});
					if (ambientSound != null) {
						ambientSound.Start();
						ambientSound.PlaybackPosition = ambientSound.SoundLengthSeconds * (float)Api.World.Rand.NextDouble();
					}
				}
			} else {
				ambientSound?.Stop();
				ambientSound?.Dispose();
				ambientSound = null;
			}
		}
		#endregion

		#region Music
		public override void OnBlockUnloaded() {
			ToggleAmbientSounds(false);
		}
		#endregion
	}
}