using System.Security.Cryptography;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VSKingdom.Utilities;

namespace VSKingdom {
	public class EntityBehaviorClockwins : EntityBehavior {
		public EntityBehaviorClockwins(Entity entity) : base(entity) { }
		protected bool isPlayer;
		protected bool isSentry;
		protected bool isOthers;
		protected bool isAnimal;
		protected long schizoMs;
		protected long infectMs;
		protected long darkness { get => entity.World.BlockAccessor.GetLightLevel(entity.ServerPos.AsBlockPos, EnumLightLevelType.MaxLight); }
		protected ITreeAttribute Clockwins { get => entity.WatchedAttributes.GetOrAddTreeAttribute("clockwins"); }
		protected double Infection { get => Clockwins.GetDouble("virusSpread"); set => Clockwins.SetDouble("virusSpread", value); }

		public override string PropertyName() {
			return "DiseaseClockwins";
		}

		public override void AfterInitialized(bool onFirstSpawn) {
			base.AfterInitialized(onFirstSpawn);
			schizoMs = 0;
			isPlayer = entity is EntityPlayer;
			isSentry = entity is EntitySentry;
			isOthers = entity is EntityHumanoid;
			isAnimal = entity is EntityAgent;
			Infection = 0;
		}

		public override void OnGameTick(float dt) {
			if (isPlayer) {
				if (schizoMs < entity.World.ElapsedMilliseconds) {
					double infection = (float)Infection;
					string schizosfx = null;
					// Increase frequency of schizophrenic visions and sounds.
					schizoMs = entity.World.ElapsedMilliseconds + entity.World.Rand.Next((int)(120000 * infection), 120000);
					// Play sound in random direction.
					if (infection > 0.9) {
						schizosfx = "vskingdom:schizo/screaming";
					} else if (infection > 0.7) {
						schizosfx = "vskingdom:schizo/footsteps";
					}
					if (schizosfx != null && entity.World.GetPlayersAround(entity.ServerPos.XYZ, 36f, 36f).Length < 2 && entity.World.Rand.NextDouble() < infection) {
						double schizoX = entity.ServerPos.X + entity.World.Rand.Next(-10, 10);
						double schizoY = entity.ServerPos.Y + entity.World.Rand.Next(-5, 5);
						double schizoZ = entity.ServerPos.Z + entity.World.Rand.Next(-10, 10);
						double rangeTo = entity.ServerPos.DistanceTo(new Vec3d(schizoX, schizoY, schizoZ));
						entity.World.PlaySoundAt(new AssetLocation(schizosfx), schizoX, schizoY, schizoZ, null, true, (float)rangeTo);
					}
				}
				if (darkness < 7) {
					// Very slowly increase infection if in the darkness.
					Infection += 0.00001;
				}
				return;
			}
			if (entity.Alive) {
				return;
			}
			if (isPlayer) {
				var player = entity as EntityPlayer;
				EntityProperties properties = entity.World.GetEntityType(new AssetLocation("vskingdom:zombie-masc"));
				EntityZombie zombie = (EntityZombie)entity.Api.World.ClassRegistry.CreateEntity(properties);
				zombie.zombified = true;
				zombie.ServerPos.SetFrom(player.ServerPos);
				zombie.Pos.SetFrom(player.ServerPos);
				entity.Api.World.SpawnEntity(zombie);
				for (int i = 0; i < player.GearInventory.Count; i++) {
					if (!player.GearInventory[i].Empty && player.GearInventory[i]?.Itemstack.Item is ItemWearable) {
						player.GearInventory[i].TryPutInto(entity.Api.World, zombie.gearInv[i], 1);
					}
				}
				entity.WatchedAttributes.RemoveAttribute("clockwins");
				entity.RemoveBehavior(this);
				return;
			}
			Infection += 0.01;
			if (Infection < 1 || darkness > 10) {
				return;
			}
			if (isSentry) {
				var sentry = entity as EntitySentry;
				EntityProperties properties = entity.World.GetEntityType(new AssetLocation($"vskingdom:zombie-{sentry.Code.EndVariant() ?? "masc"}"));
				EntityZombie zombie = (EntityZombie)entity.Api.World.ClassRegistry.CreateEntity(properties);
				zombie.zombified = true;
				zombie.ServerPos.SetFrom(sentry.ServerPos);
				zombie.Pos.SetFrom(sentry.ServerPos);
				entity.Api.World.SpawnEntity(zombie);
				for (int i = 0; i < sentry.gearInv.Count; i++) {
					if (!sentry.gearInv[i].Empty) {
						sentry.gearInv[i].TryPutInto(entity.Api.World, zombie.gearInv[i], sentry.gearInv[i].StackSize);
					}
				}
				entity.Die(EnumDespawnReason.Removed);
				return;
			}
			if (isOthers) {
				var others = entity as EntityHumanoid;
				EntityProperties properties = entity.World.GetEntityType(new AssetLocation("game:drifter-normal"));
				EntityDrifter zombie = (EntityDrifter)entity.Api.World.ClassRegistry.CreateEntity(properties);
				zombie.ServerPos.SetFrom(others.ServerPos);
				zombie.Pos.SetFrom(others.ServerPos);
				if (entity.Api.World is IServerWorldAccessor && !others.GearInventory.Empty) {
					(others.GearInventory as InventoryBase)?.DropAll(others.ServerPos.XYZ.Add(0.5, 0.5, 0.5));
				}
				entity.Api.World.SpawnEntity(zombie);
				entity.Die(EnumDespawnReason.Removed);
				return;
			}
		}

		public void Worsen(double amount) {
			Infection = +Infection + amount;
		}
	}
}
