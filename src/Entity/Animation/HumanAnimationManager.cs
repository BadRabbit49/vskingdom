using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VSKingdom {
	public class HumanAnimationManager : AnimationManager {
		public string Personality;
		public HashSet<string> PersonalizedAnimations = new HashSet<string>(new string[] { "idle", "walk", "sprint", "idle2", "idle3", "hit" });

		public override bool StartAnimation(string configCode) {
			if (PersonalizedAnimations.Contains(configCode.ToLowerInvariant())) {
				if (Personality == "formal" || Personality == "rowdy" || Personality == "lazy") {
					StopAnimation(Personality + "idle");
					StopAnimation(Personality + "idle2");
					StopAnimation(Personality + "idle3");
				}
				return StartAnimation(new AnimationMetaData() {
					Animation = Personality + configCode,
					Code = Personality + configCode,
					BlendMode = EnumAnimationBlendMode.Average,
					EaseOutSpeed = 10000,
					EaseInSpeed = 10000
				}.Init());
			}
			return base.StartAnimation(configCode);
		}

		public override bool StartAnimation(AnimationMetaData animdata) {
			if (Personality == "formal" || Personality == "rowdy" || Personality == "lazy") {
				StopAnimation(Personality + "idle");
				StopAnimation(Personality + "idle2");
				StopAnimation(Personality + "idle3");
			}
			if (PersonalizedAnimations.Contains(animdata.Animation.ToLowerInvariant())) {
				animdata = animdata.Clone();
				animdata.Animation = Personality + animdata.Animation;
				animdata.Code = animdata.Animation;
				animdata.CodeCrc32 = AnimationMetaData.GetCrc32(animdata.Code);
			}
			return base.StartAnimation(animdata);
		}

		public override void StopAnimation(string code) {
			base.StopAnimation(code);
			base.StopAnimation(Personality + code);
		}

		public override void OnAnimationStopped(string code) {
			base.OnAnimationStopped(code);
			if (entity.Alive && ActiveAnimationsByAnimCode.Count == 0) {
				StartAnimation(new AnimationMetaData() { Code = "idle", Animation = "idle", EaseOutSpeed = 10000, EaseInSpeed = 10000 });
			}
		}
	}

	public class HumanPersonality {
		public float ChorldDelayMul = 1f;
		public float PitchModifier = 1f;
		public float VolumneModifier = 1f;
		public HumanPersonality(float chordDelayMul, float pitchModifier, float volumneModifier) {
			ChorldDelayMul = chordDelayMul;
			PitchModifier = pitchModifier;
			VolumneModifier = volumneModifier;
		}
	}
}