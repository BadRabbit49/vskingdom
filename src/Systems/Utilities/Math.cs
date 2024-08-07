﻿using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using System;
using System.Diagnostics;

namespace VSKingdom {
	internal static class MathUtility {
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
			// Do a line Trace into the target, see if there are any entities in the way.
			entity1.World.RayTraceForSelection(entity1.ServerPos.XYZ.AddCopy(entity1.LocalEyePos), entity2.ServerPos.XYZ.AddCopy(entity2.LocalEyePos), ref blockSel, ref entitySel);
			if (blockSel.Block.SideIsSolid(blockSel.Position, blockSel.Face.Index) || entitySel.Entity != entity2) {
				return false;
			}
			return true;
		}

		public static int  GetRandom(int num, int low = 0) {
			Random rnd = new Random();
			return rnd.Next(low, num);
		}

		public static string GetRandom(string[] strings) {
			Random rnd = new Random();
			return strings[rnd.Next(0, strings.Length - 1)];
		}

		public static TimeSpan Time(Action action) {
			Stopwatch stopwatch = Stopwatch.StartNew();
			action();
			stopwatch.Stop();
			return stopwatch.Elapsed;
		}
	}
}