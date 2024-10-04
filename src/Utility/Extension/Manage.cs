using Vintagestory.API.Common;

namespace VSKingdom.Extension {
	internal static class ManageExtension {
		public static void ReplaceAnims(this IAnimationManager manager, string oldCode, string newCode) {
			manager.ActiveAnimationsByAnimCode[oldCode] = new AnimationMetaData {
				Code = newCode,
				Animation = newCode,
				AnimationSpeed = 1,
				EaseInSpeed = 999f,
				EaseOutSpeed = 999f,
				BlendMode = EnumAnimationBlendMode.Average,
				MulWithWalkSpeed = true,
			}.Init();
		}
	}
}