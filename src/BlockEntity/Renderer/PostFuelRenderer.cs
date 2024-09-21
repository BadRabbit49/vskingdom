using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class PostFuelRenderer : IRenderer, ITexPositionSource {
		private ICoreClientAPI capi;
		private BlockPos pos;

		MeshRef workItemMeshRef { get; set; }

		MeshRef emberQuadRef;
		MeshRef coalQuadRef;

		ItemStack stack;
		float fuelLevel;
		bool burning;

		TextureAtlasPosition metalTexPos;
		TextureAtlasPosition fuelsTexPos;
		TextureAtlasPosition burnsTexPos;
		TextureAtlasPosition emberTexPos;

		int textureId;

		string fuelCode;
		string contents;
		ITexPositionSource tmpTextureSource;

		Matrixf ModelMat = new Matrixf();

		public double RenderOrder { get => 0.5; }

		public int RenderRange { get => 24; }

		public Size2i AtlasSize { get => capi.BlockTextureAtlas.Size; }

		public TextureAtlasPosition this[string textureCode] { get => tmpTextureSource[contents]; }

		public PostFuelRenderer(BlockPos pos, AssetLocation code, ICoreClientAPI capi) {
			this.pos = pos;
			this.capi = capi;

			Block block = capi.World.GetBlock(code);

			metalTexPos = capi.BlockTextureAtlas.GetPosition(block, "metal");
			fuelsTexPos = capi.BlockTextureAtlas.GetPosition(block, "fuels");
			burnsTexPos = capi.BlockTextureAtlas.GetPosition(block, "burns");
			emberTexPos = capi.BlockTextureAtlas.GetPosition(block, "ember");

			MeshData emberMesh = QuadMeshUtil.GetCustomQuadHorizontal(3 / 16f, 0, 3 / 16f, 10 / 16f, 10 / 16f, 255, 255, 255, 255);

			for (int i = 0; i < emberMesh.Uv.Length; i += 2) {
				emberMesh.Uv[i + 0] = emberTexPos.x1 + emberMesh.Uv[i + 0] * 32f / AtlasSize.Width;
				emberMesh.Uv[i + 1] = emberTexPos.y1 + emberMesh.Uv[i + 1] * 32f / AtlasSize.Height;
			}
			emberMesh.Flags = new int[] { 128, 128, 128, 128 };

			MeshData coalMesh = QuadMeshUtil.GetCustomQuadHorizontal(3 / 16f, 0, 3 / 16f, 10 / 16f, 10 / 16f, 255, 255, 255, 255);

			for (int i = 0; i < coalMesh.Uv.Length; i += 2) {
				coalMesh.Uv[i + 0] = fuelsTexPos.x1 + coalMesh.Uv[i + 0] * 32f / AtlasSize.Width;
				coalMesh.Uv[i + 1] = fuelsTexPos.y1 + coalMesh.Uv[i + 1] * 32f / AtlasSize.Height;
			}

			emberQuadRef = capi.Render.UploadMesh(emberMesh);
			coalQuadRef = capi.Render.UploadMesh(coalMesh);
		}

		public void SetContents(ItemStack stack, float fuelLevel, bool burning, bool regen) {
			this.stack = stack;
			this.fuelLevel = fuelLevel;
			this.burning = burning;
			if (regen) {
				RegenMesh();
			}
		}

		void RegenMesh() {
			workItemMeshRef?.Dispose();
			workItemMeshRef = null;
			if (stack == null || !fuelsCodes.ContainsKey(stack.Collectible.Code.Path)) {
				return;
			}
			contents = fuelsCodes[stack.Collectible.Code.Path][0];
			fuelCode = fuelsCodes[stack.Collectible.Code.Path][1];
			MeshData modelData = null;
			Shape shapeBase = Shape.TryGet(capi, "vskingdom:shapes/block/outposts/contents_" + fuelCode + ".json");
			tmpTextureSource = capi.Tesselator.GetTextureSource(stack.Item);
			textureId = tmpTextureSource[contents].atlasTextureId;
			capi.Tesselator.TesselateShape("postcontents", shapeBase, out modelData, this, null, 0, 0, 0, stack.StackSize);
			if (modelData != null) {
				workItemMeshRef = capi.Render.UploadMesh(modelData);
			} else {
				capi.Logger.Error($"shapeBase was null, couldn't find {fuelCode} at: {new string("vskingdom:shapes/block/outposts/contents_" + fuelCode + ".json")}");
			}
		}

		public void OnRenderFrame(float deltaTime, EnumRenderStage stage) {
			if (stack == null && fuelLevel == 0) {
				return;
			}

			IRenderAPI rpi = capi.Render;
			IClientWorldAccessor worldAccess = capi.World;
			Vec3d camPos = worldAccess.Player.Entity.CameraPos;

			rpi.GlDisableCullFace();
			IStandardShaderProgram prog = rpi.StandardShader;
			prog.Use();
			prog.RgbaAmbientIn = rpi.AmbientColor;
			prog.RgbaFogIn = rpi.FogColor;
			prog.FogMinIn = rpi.FogMin;
			prog.FogDensityIn = rpi.FogDensity;
			prog.RgbaTint = ColorUtil.WhiteArgbVec;
			prog.DontWarpVertices = 0;
			prog.AddRenderFlags = 0;
			prog.ExtraGodray = 0;
			prog.OverlayOpacity = 0;

			if (stack != null && workItemMeshRef != null) {
				Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
				float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(1100);
				int extraGlow = 255;
				prog.NormalShaded = 1;
				prog.RgbaLightIn = lightrgbs;
				prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], extraGlow / 255f);
				prog.ExtraGlow = extraGlow;
				prog.Tex2D = textureId;
				prog.ModelMatrix = ModelMat.Identity().Translate(pos.X - camPos.X, pos.Y - camPos.Y + 10 / 16f + fuelLevel * 0.65f, pos.Z - camPos.Z).Values;
				prog.ViewMatrix = rpi.CameraMatrixOriginf;
				prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
				rpi.RenderMesh(workItemMeshRef);
			}
			if (fuelLevel > 0) {
				Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
				long seed = capi.World.ElapsedMilliseconds + pos.GetHashCode();
				float flicker = (float)(Math.Sin(seed / 40.0) * 0.2f + Math.Sin(seed / 220.0) * 0.6f + Math.Sin(seed / 100.0) + 1) / 2f;
				if (burning) {
					float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(1200);
					glowColor[0] *= 1f - flicker * 0.15f;
					glowColor[1] *= 1f - flicker * 0.15f;
					glowColor[2] *= 1f - flicker * 0.15f;
					prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], 1);
				} else {
					prog.RgbaGlowIn = new Vec4f(0, 0, 0, 0);
				}
				prog.NormalShaded = 0;
				prog.RgbaLightIn = lightrgbs;
				int glow = 255 - (int)(flicker * 50);
				prog.ExtraGlow = burning ? glow : 0;

				// The coal or embers.
				rpi.BindTexture2d(burning ? emberTexPos.atlasTextureId : fuelsTexPos.atlasTextureId);
				prog.ModelMatrix = ModelMat.Identity().Translate(pos.X - camPos.X, pos.Y - camPos.Y + 10 / 16f + fuelLevel * 0.65f, pos.Z - camPos.Z).Values;
				prog.ViewMatrix = rpi.CameraMatrixOriginf;
				prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
				rpi.RenderMesh(burning ? emberQuadRef : coalQuadRef);
			}
			prog.Stop();
		}

		public void Dispose() {
			capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
			emberQuadRef?.Dispose();
			coalQuadRef?.Dispose();
			workItemMeshRef?.Dispose();
		}
	}
}
