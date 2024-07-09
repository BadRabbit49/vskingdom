using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using System.Collections.Generic;
using Vintagestory.API.Client;
using System;

namespace VSKingdom {
	public class BlockBehaviorResupply : BlockEntityBehavior {
		public BlockBehaviorResupply(BlockEntity blockentity) : base(blockentity) {
			bePost = blockentity as BlockEntityPost;
		}

		public bool fillAmmo;
		public int ammoAmnt;
		public int healAmnt;
		public Vec3d Position => Blockentity.Pos.ToVec3d();
		public BlockEntityPost bePost;
		public ILoadedSound ambientSound;
		
		private bool IsSnowing { get => Api.World.BlockAccessor.GetClimateAt(Blockentity.Pos, EnumGetClimateMode.NowValues).Temperature < 10; }
		
		private bool IsDarkout { get => Api.World.BlockAccessor.GetLightLevel(bePost.Pos, EnumLightLevelType.TimeOfDaySunLight) < 10; }

		private bool HasNearbySoldiers {
			get {
				for (int s = 0; s < bePost.EntityUIDs.Count - 1; s++) {
					if (Api.World.GetEntityById(bePost.EntityUIDs[s]).Pos.DistanceTo(Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5)) < bePost.areasize) {
						return true;
					}
				}
				return false;
			}
		}
		public override void Initialize(ICoreAPI api, JsonObject properties) {
			base.Initialize(api, properties);
			fillAmmo = bePost.metlTier >= 4;
			ammoAmnt = bePost.metlTier;
			healAmnt = bePost.metlTier;
			/** TODO: SETUP LISTENER TO PASSIVELY REGEN HEALTH AND RESPAWN TOKENS OVER TIME!!! **/
			if (Blockentity.Api.Side == EnumAppSide.Universal && bePost != null) {
				Blockentity.RegisterGameTickListener(OnTickOffset, 60000);
			}
		}

		private void OnTickOffset(float dt) {
			if (!bePost.fireLive) {
				return;
			}
			if (bePost.EntityUIDs.Count == 0) {
				return;
			}
			// Try to heal, revive, and resupply all soldiers in the list if in nearby range.
			if (HasNearbySoldiers) {
				List<long> soldierList = bePost.EntityUIDs;
				foreach (long soldierID in soldierList) {
					var soldier = Api.World.GetEntityById(soldierID) as EntitySentry;
					if (soldier.Pos.DistanceTo(Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5)) <= bePost.areasize) {
						// Heal soldier by given amount.
						soldier.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = healAmnt > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healAmnt));
						// Refill ammo by given amount if enabled.
						if (fillAmmo && !Api.World.Config.GetAsBool("InfiniteAmmo")) {
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
						Position = bePost.Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
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