using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	internal static class MathUtility {
		public static double GetLookAtPitch(this Vec3d from, Vec3d toPos) {
			double dx = (from.X - toPos.X);
			double dz = (from.Z - toPos.Z);
			return Math.Atan2(dz, dx) * 180.0 / Math.PI;
		}

		public static double GetLookAtYaw(this Vec3d from, Vec3d toPos) {
			double dy = (from.Y - toPos.Y);
			return (Math.Atan(dy) * 180 / Math.PI);
		}

		public static IPlayer GetClosest(Vec3d pos, IPlayer[] players) {
			IPlayer closest = players[0];
			double bestDist = pos.SquareDistanceTo(players[0].Entity.ServerPos);
			for (int i = 0; i < players.Length; i++) {
				if (pos.SquareDistanceTo(players[i].Entity.ServerPos) < bestDist) {
					closest = players[i];
					bestDist = pos.SquareDistanceTo(players[i].Entity.ServerPos);
				}
			}
			return closest;
		}
		
		public static Entity GetClosest(Vec3d pos, Entity[] entities) {
			Entity closest = entities[0];
			double bestDist = pos.SquareDistanceTo(entities[0].ServerPos);
			for (int i = 0; i < entities.Length; i++) {
				if (pos.SquareDistanceTo(entities[i].ServerPos) < bestDist) {
					closest = entities[i];
					bestDist = pos.SquareDistanceTo(entities[i].ServerPos);
				}
			}
			return closest;
		}

		public static bool CanSeeEnt(Entity entity1, Entity entity2) {
			EntitySelection entitySel = new EntitySelection();
			BlockSelection blockSel = new BlockSelection();
			BlockFilter bFilter = (pos, block) => (block == null || block.RenderPass != EnumChunkRenderPass.Transparent);
			EntityFilter efilter = (e) => (e.IsInteractable || e.EntityId != entity1.EntityId);
			// Do a line Trace into the target, see if there are any entities in the way.
			entity1.World.RayTraceForSelection(entity1.ServerPos.XYZ.AddCopy(entity1.LocalEyePos), entity2.ServerPos.XYZ.AddCopy(entity2.LocalEyePos), ref blockSel, ref entitySel, bFilter);
			if (blockSel.Block != null) {
				return !blockSel.Block.SideIsSolid(blockSel.Position, blockSel.Face.Index);
			}
			if (entitySel.Entity != null) {
				return entitySel.Entity.EntityId == entity2.EntityId;
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