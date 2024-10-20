using System;
using System.Linq;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;
using Vintagestory.API.Util;

namespace VSKingdom.Utilities {
	internal static class TracingUtil {
		private static bool IgnoreAssets;
		private static bool FriendlyFire;
		private static long SourceEntity;
		private static long TargetEntity;
		private static AssetLocation[] IgnoredArray = null;
		private static Entity[] AllyEntities = new Entity[0];
		private static EntitySelection EntitySelect = null;
		private static BlockSelection BlocksSelect = null;

		private static bool ShotAtFilter(BlockPos pos, Block blocks) {
			if (blocks.BlockMaterial == EnumBlockMaterial.Plant || blocks.BlockMaterial == EnumBlockMaterial.Leaves) {
				return true;
			}
			return blocks.BlockId != 0;
		}

		private static bool BlocksFilter(BlockPos pos, Block blocks) {
			/** Ignore Transparent Blocks like Glass! **/
			if (blocks.RenderPass == EnumChunkRenderPass.Transparent || blocks.BlockMaterial == EnumBlockMaterial.Glass) {
				return false;
			}
			if (blocks.BlockMaterial == EnumBlockMaterial.Plant || blocks.BlockMaterial == EnumBlockMaterial.Leaves) {
				return true;
			}
			return blocks.BlockId != 0;
		}

		private static bool EntityFilter(Entity entity) {
			/** Ignore Entity doing the Raycast! **/
			if (entity.EntityId == SourceEntity) {
				return false;
			}
			if (entity.EntityId == TargetEntity) {
				return true;
			}
			if (FriendlyFire) {
				if (AllyEntities.Contains(entity)) {
					return true;
				}
			}
			if (IgnoreAssets) {
				if (IgnoredArray.Contains(entity.Code)) {
					return false;
				}
			}
			return true;
		}
		
		public static bool CanSeeEntity(Entity entity, Entity target, AssetLocation[] ignore = null) {
			SourceEntity = entity.EntityId;
			TargetEntity = target.EntityId;
			IgnoreAssets = ignore != null;
			IgnoredArray = ignore;
			FriendlyFire = false;
			var eyesPos = entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos);
			entity.World.RayTraceForSelection(eyesPos, target.ServerPos.XYZ, ref BlocksSelect, ref EntitySelect, BlocksFilter, EntityFilter);
			if (BlocksSelect != null || EntitySelect == null) {
				return false;
			}
			return EntitySelect.Entity.EntityId == TargetEntity;
		}

		public static bool CanHitEntity(Entity entity, Entity target, AssetLocation[] ignore = null, bool allies = false) {
			SourceEntity = entity.EntityId;
			TargetEntity = target.EntityId;
			IgnoreAssets = ignore != null;
			IgnoredArray = ignore;
			FriendlyFire = allies;
			if (allies && entity is EntitySentry sentry) {
				float horDist = (float)target.ServerPos.HorDistanceTo(entity.ServerPos);
				float verDist = (float)target.ServerPos.DistanceTo(entity.ServerPos);
				cachedKingdom = sentry.cachedData.kingdomGUID;
				cachedLeaders = sentry.cachedData.leadersGUID;
				cachedEnemies = sentry.cachedData.enemiesLIST;
				cachedOutlaws = sentry.cachedData.outlawsLIST;
				AllyEntities = entity.World.GetEntitiesAround(entity.ServerPos.XYZ, horDist, verDist, CheckIsEnemy);
			}
			var eyesPos = entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos);
			entity.World.RayTraceForSelection(eyesPos, target.ServerPos.XYZ, ref BlocksSelect, ref EntitySelect, ShotAtFilter, EntityFilter);
			// Make sure the target isn't obstructed by other entities, but if it IS then make sure it's okay to hit them.
			if (BlocksSelect != null || EntitySelect == null) {
				return false;
			}
			if (EntitySelect.Entity.EntityId == TargetEntity) {
				return true;
			}
			return false;
		}

		public static Vec3d GetHitTarget(Entity entity, Vec3d target) {
			SourceEntity = entity.EntityId;
			IgnoreAssets = false;
			IgnoredArray = null;
			FriendlyFire = false;
			var eyesPos = entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos);
			entity.World.RayTraceForSelection(eyesPos, target, ref BlocksSelect, ref EntitySelect, BlocksFilter, EntityFilter);
			return BlocksSelect?.Position?.ToVec3d() ?? EntitySelect?.Position ?? target;
		}

		private static string cachedKingdom;
		private static string cachedLeaders;
		private static string[] cachedEnemies;
		private static string[] cachedOutlaws;

		private static bool CheckIsEnemy(Entity target) {
			if (target.WatchedAttributes.HasAttribute(KingdomGUID)) {
				return cachedEnemies.Contains(target.WatchedAttributes.GetKingdom());
			}
			if (target.WatchedAttributes.HasAttribute(LeadersGUID)) {
				return cachedOutlaws.Contains(target.WatchedAttributes.GetKingdom());
			}
			if (target is EntityPlayer player) {
				return cachedOutlaws.Contains(player.PlayerUID);
			}
			if (target.WatchedAttributes.HasAttribute("domesticationstatus")) {
				target.WatchedAttributes.SetString(LeadersGUID, target.WatchedAttributes.GetTreeAttribute("domesticationstatus").GetString("owner"));
			}
			return false;
		}
	}
}