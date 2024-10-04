using System;
using Vintagestory.API.MathTools;
using VSKingdom.Extension;

namespace VSKingdom.Utilities {
	internal static class ParadesUtil {
		private const float dir_EAST = (2 * GameMath.PI);
		private const float dirNEAST = (1 * GameMath.PI) / 4;
		private const float dirNORTH = (1 * GameMath.PI) / 2;
		private const float dirNWEST = (3 * GameMath.PI) / 4;
		private const float dir_WEST = (1 * GameMath.PI);
		private const float dirSWEST = (5 * GameMath.PI) / 4;
		private const float dirSOUTH = (3 * GameMath.PI) / 2;
		private const float dirSEAST = (7 * GameMath.PI) / 4;

		public static float SnapRadians(float yawDir) {
			if (yawDir <= 0.5f || yawDir > 6.0f) {
				return dir_EAST;
			} else if (yawDir >= 0.5f && yawDir < 1.3f) {
				return dirNEAST;
			} else if (yawDir >= 1.3f && yawDir < 2.1f) {
				return dirNORTH;
			} else if (yawDir >= 2.1f && yawDir < 2.9f) {
				return dirNWEST;
			} else if (yawDir >= 2.9f && yawDir < 3.7f) {
				return dir_WEST;
			} else if (yawDir >= 3.7f && yawDir < 4.5f) {
				return dirSWEST;
			} else if (yawDir >= 4.5f && yawDir < 5.2f) {
				return dirSOUTH;
			} else if (yawDir >= 5.2f && yawDir < 6.0f) {
				return dirSEAST;
			}
			return 0;
		}

		public static Vec3d FormsOffset(int formSizes, int formIndex, float leaderYaw) {
			(int, int) _raf = RankAndFile(formSizes);
			int _maxRank = _raf.Item1;
			int _maxFile = _raf.Item2;
			int _members = 0;
			for (int x = 0; x < _maxRank; ++x) {
				for (int y = 0; y < _maxFile; ++y) {
					_members++;
					if (_members == formIndex) {
						return new Vec3d().FormationOffset((y - (int)MathF.Round(_maxRank / 2)), 0, (x + 1), SnapRadians(leaderYaw));
					}
				}
			}
			return new Vec3d(-1, 0, 0);
		}

		public static (int, int) RankAndFile(int formSizes) {
			int _rows = 0;
			int _cols = 0;
			switch (formSizes) {
				case 0: _rows = 0; _cols = 0; break;
				case 1: _rows = 1; _cols = 1; break;
				case 2: _rows = 2; _cols = 1; break;
				case 3: _rows = 3; _cols = 1; break;
				case 4: _rows = 2; _cols = 2; break;
				case 5: _rows = 3; _cols = 2; break;
				case 6: _rows = 3; _cols = 2; break;
				case 7: _rows = 4; _cols = 2; break;
				case 8: _rows = 4; _cols = 2; break;
				case 9: _rows = 3; _cols = 3; break;
				default:
					_rows = (int)Math.Floor((double)Math.Sqrt(formSizes));
					_cols = (int)Math.Floor((double)Math.Sqrt(formSizes));
					break;
			}
			return (_rows, _cols);
		}
    }
}
