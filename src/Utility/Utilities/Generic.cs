using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VSKingdom.Utilities {
	internal static class GenericUtil {
		public static string GetKingdom(this SyncedTreeAttribute tree) {
			return tree.GetString(KingdomUID, CommonerID);
		}

		public static string GetCulture(this SyncedTreeAttribute tree) {
			return tree.GetString(CultureUID, SeraphimID);
		}

		public static string GetLeaders(this SyncedTreeAttribute tree) {
			return tree.GetString(LeadersUID);
		}

		public static double GetLookAtPitch(this Vec3d from, Vec3d toPos) {
			double dx = (from.X - toPos.X);
			double dz = (from.Z - toPos.Z);
			return Math.Atan2(dz, dx) * 180.0 / Math.PI;
		}

		public static double GetLookAtYaw(this Vec3d from, Vec3d toPos) {
			double dy = (from.Y - toPos.Y);
			return (Math.Atan(dy) * 180 / Math.PI);
		}

		public static bool CanSeeEnt(Entity entity1, Entity entity2) {
			EntitySelection entitySel = new EntitySelection();
			BlockSelection blockSel = new BlockSelection();
			BlockFilter bFilter = (pos, block) => (block == null || block.RenderPass != EnumChunkRenderPass.Transparent);
			EntityFilter eFilter = (e) => (e.IsInteractable || e.EntityId != entity1.EntityId);
			// Do a line Trace into the target, see if there are any entities in the way.
			entity1.World.RayTraceForSelection(entity1.ServerPos.XYZ.AddCopy(entity1.LocalEyePos), entity2.ServerPos.XYZ.AddCopy(entity2.LocalEyePos), ref blockSel, ref entitySel, bFilter, eFilter);
			if (blockSel?.Block != null) {
				return !blockSel.Block?.SideIsSolid(blockSel.Position, blockSel.Face.Index) ?? true;
			}
			if (entitySel?.Entity != null) {
				return entitySel.Entity?.EntityId == entity2.EntityId;
			}
			return true;
		}

		public static bool CanSeePos(IWorldAccessor world, Vec3d pos1, Vec3d pos2) {
			EntitySelection entitySel = new EntitySelection();
			BlockSelection blockSel = new BlockSelection();
			BlockFilter bFilter = (pos, block) => (block == null || block.RenderPass == EnumChunkRenderPass.Transparent || block.IsLiquid());
			// Do a line Trace into the target, see if there are any blocks in the way.
			world.RayTraceForSelection(pos1, pos2, ref blockSel, ref entitySel, bFilter);
			if (blockSel.Block != null) {
				return !blockSel.Block.SideIsSolid(blockSel.Position, blockSel.Face.Index);
			}
			return true;
		}

		public static int GetRandom(int num, int low = 0) {
			Random rnd = new Random();
			return rnd.Next(low, num);
		}

		public static string GetRandom(string[] strings) {
			Random rnd = new Random();
			return strings[rnd.Next(0, strings.Length - 1)];
		}

		public static string RandomGuid(string guid, int size, string[] LIST = null) {
			if (guid != null && guid.Length == size) {
				return guid;
			}
			bool repeating = true;
			Random rnd = new Random();
			StringBuilder strBuilder = new StringBuilder();
			while (repeating) {
				strBuilder.Clear();
				Enumerable
					.Range(65, 26).Select(e => ((char)e).ToString())
					.Concat(Enumerable.Range(97, 26).Select(e => ((char)e).ToString()))
					.Concat(Enumerable.Range(0, 7).Select(e => e.ToString()))
					.OrderBy(e => Guid.NewGuid()).Take(size)
					.ToList().ForEach(e => strBuilder.Append(e));
				if (LIST != null) {
					repeating = LIST.Contains(strBuilder.ToString());
				} else {
					repeating = false;
				}
			}
			return strBuilder.ToString();
		}
	}
}