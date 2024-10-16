using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VSKingdom {
	public class SentryTalkUtils : EntityTalkUtil {
		protected float soundModifier = 1f;
		protected float talkingChance = 0.0005f;
		protected EntitySentry sentry;
		protected List<SlidingPitchSound> slidingPitchesSounds = new List<SlidingPitchSound>();

		public SentryTalkUtils(ICoreAPI api, Entity entity) : base (api, entity) {
			sapi = api as ICoreServerAPI;
			capi = api as ICoreClientAPI;
			sentry = entity as EntitySentry;
			TalkSpeed = DefaultTalkSpeed();
			capi?.Event.RegisterRenderer(new DummyRenderer { action = OnRenderTick }, EnumRenderStage.Before, "talkfasttilk");
			if (api.Side == EnumAppSide.Client) {
				SoundParams param = new SoundParams { Location = soundName, DisposeOnFinish = true, ShouldLoop = false };
				ILoadedSound loadedSound = capi.World.LoadSound(param);
				soundLength = loadedSound?.SoundLengthSeconds ?? 0.1f;
				loadedSound?.Dispose();
			}
		}

		private void OnRenderTick(float dt) {
			for (int i = 0; i < slidingPitchesSounds.Count; i++) {
				SlidingPitchSound slidingPitchSound = slidingPitchesSounds[i];
				if (slidingPitchSound.sound.HasStopped) {
					stoppedSlidingSounds.Add(slidingPitchSound);
					continue;
				}
				float num = (float)(capi.World.ElapsedMilliseconds - slidingPitchSound.startMs) / 1000f;
				float length = slidingPitchSound.length;
				float t = GameMath.Min(1f, num / length);
				float num2 = GameMath.Lerp(slidingPitchSound.startPitch, slidingPitchSound.endPitch, t);
				float num3 = GameMath.Lerp(slidingPitchSound.StartVolumne, slidingPitchSound.EndVolumne, t);
				if (num > length) {
					num3 -= (num - slidingPitchSound.length) * 5f;
				}
				slidingPitchSound.Vibrato = slidingPitchSound.TalkType == EnumTalkType.Death || slidingPitchSound.TalkType == EnumTalkType.Thrust;
				if (slidingPitchSound.TalkType == EnumTalkType.Thrust && (double)num > 0.15) {
					slidingPitchSound.sound.Stop();
					continue;
				}
				if (num3 <= 0f) {
					slidingPitchSound.sound.FadeOutAndStop(0f);
					continue;
				}
				slidingPitchSound.sound.SetPitch(num2 + (slidingPitchSound.Vibrato ? ((float)Math.Sin(num * 8f) * 0.05f) : 0f));
				slidingPitchSound.sound.FadeTo(num3, 0.1f, delegate { });
			}
			for (int i = 0; i < stoppedSlidingSounds.Count; i++) {
				slidingPitchesSounds.Remove(slidingPitchesSounds[i]);
			}
		}

		public override void SetModifiers(float chordDelayMul = 1f, float pitchModifier = 1f, float volumneModifier = 1f) {
			this.chordDelayMul = chordDelayMul;
			this.pitchModifier = pitchModifier;
			this.soundModifier = volumneModifier;
			TalkSpeed = DefaultTalkSpeed();
			EnumTalkType[] array = TalkSpeed.Keys.ToArray();
			foreach (EnumTalkType key in array) {
				TalkSpeed[key] = Math.Max(0.06f, TalkSpeed[key] * chordDelayMul);
			}
		}

		public override void OnGameTick(float dt) {
			float num = 0.1f + (float)(capi.World.Rand.NextDouble() * capi.World.Rand.NextDouble()) / 2f;
			if (lettersLeftToTalk > 0) {
				chordDelay -= dt;
				if (!(chordDelay < 0f)) { return; }
				chordDelay = TalkSpeed[talkType];
				switch (talkType) {
					case EnumTalkType.Idle: {
							break;
						}
					case EnumTalkType.IdleShort: {
							float startPitch = 0.75f + 0.25f * (float)Rand.NextDouble();
							float endPitch2 = 0.75f + 0.25f * (float)Rand.NextDouble();
							PlaySound(startPitch, endPitch2, 0.5f, 0.5f, num);
							if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35) {
								chordDelay = 0.35f * chordDelayMul;
								currentLetterInWord = 0;
							}
							break;
						}
					case EnumTalkType.Goodbye: {
							float num7 = 1.25f - 0.6f * (float)totalLettersTalked / (float)totalLettersToTalk;
							PlaySound(num7, num7 * 0.9f, 0.35f, 0.3f, num);
							chordDelay = 0.25f * chordDelayMul;
							break;
						}
					case EnumTalkType.Death: {
							num = 2.3f;
							PlaySound(0.75f, 0.3f, 0.5f, 0.1f, num);
							break;
						}
					case EnumTalkType.Thrust: {
							num = 0.12f;
							PlaySound(0.5f, 0.8f, 0.2f, 0.5f, num);
							break;
						}
					case EnumTalkType.Shrug: {
							num = 0.6f;
							PlaySound(0.9f, 1.5f, 0.4f, 0.4f, num);
							break;
						}
					case EnumTalkType.Meet: {
							float num6 = 0.75f + 0.5f * (float)Rand.NextDouble() + (float)totalLettersTalked / (float)totalLettersToTalk / 3f;
							PlaySound(num6, num6 * 1.5f, 0.25f, 0.25f, num);
							if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35) {
								chordDelay = 0.15f * chordDelayMul;
								currentLetterInWord = 0;
							}
							break;
						}
					case EnumTalkType.Complain: {
							float num2 = 0.75f + 0.5f * (float)Rand.NextDouble();
							float num3 = num2 + 0.15f;
							num = 0.05f;
							PlaySound(num2, num3, num2, num3, num);
							if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35) {
								chordDelay = 0.45f * chordDelayMul;
								currentLetterInWord = 0;
							}
							break;
						}
					case EnumTalkType.Laugh: {
							float num8 = (float)Rand.NextDouble() * 0.1f;
							float num9 = (float)Math.Pow(Math.Min(1f, 1f / pitchModifier), 2.0);
							num = 0.1f;
							float num10 = num8 + 1.5f - (float)currentLetterInWord / (20f / num9);
							float endPitch = num10 - 0.05f;
							PlaySound(num10, endPitch, 0.6f, 0.6f, num);
							chordDelay = 0.2f * chordDelayMul * num9;
							break;
						}
					case EnumTalkType.Hurt: {
							float num4 = 0.75f + 0.5f * (float)Rand.NextDouble() + (1f - (float)totalLettersTalked / (float)totalLettersToTalk);
							num /= 4f;
							float num5 = 0.25f + (1f - (float)totalLettersTalked / (float)totalLettersToTalk) / 2f;
							PlaySound(num4, num4 - 0.2f, num5, num5, num);
							if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35) {
								chordDelay = 0.25f * chordDelayMul;
								currentLetterInWord = 0;
							}
							break;
						}
					case EnumTalkType.Hurt2: {
							float startpitch = 0.75f + 0.4f * (float)Rand.NextDouble() + (1f - (float)totalLettersTalked / (float)totalLettersToTalk);
							PlaySound(startpitch, 0.25f + (1f - (float)totalLettersTalked / (float)totalLettersToTalk) / 2.5f, num);
							if (currentLetterInWord > 1 && capi.World.Rand.NextDouble() < 0.35) {
								chordDelay = 0.2f * chordDelayMul;
								currentLetterInWord = 0;
							}
							chordDelay = 0f;
							break;
						}
					default: { break; }
				}
				if (AddSoundLengthChordDelay) {
					chordDelay += Math.Min(soundLength, num) * chordDelayMul;
				}
				lettersLeftToTalk--;
				currentLetterInWord++;
				totalLettersTalked++;
			} else if (lettersLeftToTalk == 0 && capi.World.Rand.NextDouble() < (double)talkingChance && sentry.Alive) {
				Talk(EnumTalkType.Idle);
			}
		}

		protected override void PlaySound(float startpitch, float volume, float length) {
			PlaySound(startpitch, startpitch, volume, volume, length);
		}

		protected override void PlaySound(float startPitch, float endPitch, float startvolume, float endvolumne, float length) {
			startPitch *= pitchModifier;
			endPitch *= pitchModifier;
			startvolume *= soundModifier;
			endvolumne *= soundModifier;
			SoundParams param = new SoundParams {
				Location = soundName,
				DisposeOnFinish = true,
				Pitch = startPitch,
				Volume = startvolume,
				Position = sentry.Pos.XYZ.ToVec3f().Add(0f, (float)sentry.LocalEyePos.Y, 0f),
				ShouldLoop = false,
				Range = 8f
			};
			ILoadedSound loadedSound = capi.World.LoadSound(param);
			slidingPitchesSounds.Add(new SlidingPitchSound {
				TalkType = talkType,
				startPitch = startPitch,
				endPitch = endPitch,
				sound = loadedSound,
				startMs = capi.World.ElapsedMilliseconds,
				length = length,
				StartVolumne = startvolume,
				EndVolumne = endvolumne
			});
			loadedSound.Start();
		}

		public override void Talk(EnumTalkType talkType) {
			if (sapi != null) {
				sapi.Network.BroadcastEntityPacket(sentry.EntityId, 1231, SerializerUtil.Serialize(talkType));
				return;
			}
			IClientWorldAccessor world = capi.World;
			this.talkType = talkType;
			totalLettersTalked = 0;
			currentLetterInWord = 0;
			chordDelay = TalkSpeed[talkType];
			LongNote = false;
			switch (talkType) {
				case EnumTalkType.Meet: lettersLeftToTalk = 2 + world.Rand.Next(10); break;
				case EnumTalkType.Idle: lettersLeftToTalk = 3 + world.Rand.Next(12); break;
				case EnumTalkType.Hurt: lettersLeftToTalk = 2 + world.Rand.Next(3); break;
				case EnumTalkType.Hurt2: lettersLeftToTalk = 2 + world.Rand.Next(3); break;
				case EnumTalkType.IdleShort: lettersLeftToTalk = 3 + world.Rand.Next(4); break;
				case EnumTalkType.Laugh: lettersLeftToTalk = (int)((float)(4 + world.Rand.Next(4)) * Math.Max(1f, pitchModifier)); break;
				case EnumTalkType.Purchase: lettersLeftToTalk = 2 + world.Rand.Next(2); break;
				case EnumTalkType.Complain: lettersLeftToTalk = 10 + world.Rand.Next(12); break;
				case EnumTalkType.Goodbye: lettersLeftToTalk = 2 + world.Rand.Next(2); break;
				case EnumTalkType.Death: lettersLeftToTalk = 1; break;
				case EnumTalkType.Shrug: lettersLeftToTalk = 1; break;
				case EnumTalkType.Thrust: lettersLeftToTalk = 1; break;
			}
			totalLettersToTalk = lettersLeftToTalk;
		}

		private Dictionary<EnumTalkType, float> DefaultTalkSpeed() {
			return new Dictionary<EnumTalkType, float> {
				{ EnumTalkType.Meet, 0.13f },
				{ EnumTalkType.Death, 0.3f },
				{ EnumTalkType.Idle, 0.1f },
				{ EnumTalkType.IdleShort, 0.1f },
				{ EnumTalkType.Laugh, 0.2f },
				{ EnumTalkType.Hurt, 0.07f },
				{ EnumTalkType.Hurt2, 0.07f },
				{ EnumTalkType.Goodbye, 0.07f },
				{ EnumTalkType.Complain, 0.09f },
				{ EnumTalkType.Purchase, 0.15f },
				{ EnumTalkType.Thrust, 0.15f },
				{ EnumTalkType.Shrug, 0.15f }
			};
		}
	}
}