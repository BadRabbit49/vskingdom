using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class PostFuelRenderer : IRenderer, ITexPositionSource {
		private ICoreClientAPI api;
		private BlockPos pos;

		MeshRef fuelsMeshRef;
		MeshRef burnsQuadRef;
		MeshRef emberQuadRef;
		
		ItemStack itemstack;

		TextureAtlasPosition fuelsTexPos;
		TextureAtlasPosition burnsTexPos;
		TextureAtlasPosition emberTexPos;

		bool isBurning;
		int textureID;
		float fuelLevel;
		string fuelCode;
		string contents;
		ITexPositionSource tmpTextureSource;

		Matrixf ModelMat = new Matrixf();

		public double RenderOrder { get => 0.5; }

		public int RenderRange { get => 24; }

		public Size2i AtlasSize { get => api.BlockTextureAtlas.Size; }

		public TextureAtlasPosition this[string textureCode] { get => tmpTextureSource[contents]; }

		public PostFuelRenderer(BlockPos pos, AssetLocation code, ICoreClientAPI capi) {
			this.pos = pos;
			this.api = capi;

			Block block = capi.World.GetBlock(code);

			fuelsTexPos = capi.BlockTextureAtlas.GetPosition(block, "fuels");
			burnsTexPos = capi.BlockTextureAtlas.GetPosition(block, "burns");
			emberTexPos = capi.BlockTextureAtlas.GetPosition(block, "ember");
			
			MeshData emberMesh = QuadMeshUtil.GetCustomQuadHorizontal(3 / 16f, 0, 3 / 16f, 10 / 16f, 10 / 16f, 255, 255, 255, 255);
			MeshData burnsMesh = QuadMeshUtil.GetCustomQuadHorizontal(3 / 16f, 0, 3 / 16f, 10 / 16f, 10 / 16f, 255, 255, 255, 255);

			for (int i = 0; i < burnsMesh.Uv.Length; i += 2) {
				emberMesh.Uv[i + 0] = emberTexPos.x1 + emberMesh.Uv[i + 0] * 32f / AtlasSize.Width;
				emberMesh.Uv[i + 1] = emberTexPos.y1 + emberMesh.Uv[i + 1] * 32f / AtlasSize.Height;
				burnsMesh.Uv[i + 0] = fuelsTexPos.x1 + burnsMesh.Uv[i + 0] * 32f / AtlasSize.Width;
				burnsMesh.Uv[i + 1] = fuelsTexPos.y1 + burnsMesh.Uv[i + 1] * 32f / AtlasSize.Height;
			}
			emberMesh.Flags = new int[] { 128, 128, 128, 128 };
			emberQuadRef = capi.Render.UploadMesh(emberMesh);
			burnsQuadRef = capi.Render.UploadMesh(burnsMesh);
		}

		public void SetContents(ItemStack itemstack, float fuelLevel, bool isBurning, bool regen) {
			this.itemstack = itemstack;
			this.fuelLevel = fuelLevel;
			this.isBurning = isBurning;
			if (regen) {
				RegenMesh();
			}
		}

		public void RegenMesh() {
			fuelsMeshRef?.Dispose();
			fuelsMeshRef = null;
			if (itemstack == null || !fuelsCodes.ContainsKey(itemstack.Collectible.Code.Path)) {
				return;
			}
			contents = fuelsCodes[itemstack.Collectible.Code.Path][0];
			fuelCode = fuelsCodes[itemstack.Collectible.Code.Path][1];
			MeshData modelData = null;
			Shape shapeBase = Shape.TryGet(api, OutpostContents + fuelCode + ".json");
			tmpTextureSource = api.Tesselator.GetTextureSource(itemstack.Item);
			textureID = api.Render.GetOrLoadTexture(new AssetLocation($"vskingdom:block/fuels/{fuelCode}.png"));
			// textureId = tmpTextureSource[fuelCode].atlasTextureId;
			api.Tesselator.TesselateShape("postcontents", shapeBase, out modelData, this, null, 0, 0, 0, itemstack.StackSize);
			if (modelData != null) {
				fuelsMeshRef = api.Render.UploadMesh(modelData);
			} else {
				api.Logger.Error($"shapeBase was null, couldn't find {fuelCode} at: {new string(OutpostContents + fuelCode + ".json")}");
			}
		}

		public void OnRenderFrame(float dt, EnumRenderStage stage) {
			if (itemstack == null && fuelLevel == 0) {
				return;
			}

			IRenderAPI rpi = api.Render;
			IClientWorldAccessor worldAccess = api.World;
			Vec3d camPos = worldAccess.Player.Entity.CameraPos;
			rpi.GlDisableCullFace();
			IStandardShaderProgram prog = rpi.StandardShader;
			prog.Use();
			prog.Tex2D = api.ItemTextureAtlas.AtlasTextures[0].TextureId;
			prog.DontWarpVertices = 0;
			prog.AddRenderFlags = 0;
			prog.RgbaAmbientIn = rpi.AmbientColor;
			prog.RgbaFogIn = rpi.FogColor;
			prog.FogMinIn = rpi.FogMin;
			prog.FogDensityIn = rpi.FogDensity;
			prog.RgbaTint = ColorUtil.WhiteArgbVec;
			prog.NormalShaded = 1;
			prog.ExtraGodray = 0;
			prog.SsaoAttn = 0;
			prog.AlphaTest = 0.05f;
			prog.OverlayOpacity = 0;
			Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);

			lightrgbs[0] += 20;
			lightrgbs[1] += 20;
			lightrgbs[2] += 20;

			if (itemstack != null && fuelsMeshRef != null) {
				prog.RgbaLightIn = lightrgbs;
				prog.Tex2D = textureID;


				rpi.BindTexture2d(textureID);


				prog.ModelMatrix = ModelMat.Identity().Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z).Values;
				prog.ViewMatrix = rpi.CameraMatrixOriginf;
				prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
				rpi.RenderMesh(fuelsMeshRef);
			}
			if (fuelLevel > 0) {
				long seed = api.World.ElapsedMilliseconds + pos.GetHashCode();
				float flicker = (float)(Math.Sin(seed / 40.0) * 0.2f + Math.Sin(seed / 220.0) * 0.6f + Math.Sin(seed / 100.0) + 1) / 2f;
				if (isBurning) {
					lightrgbs[0] *= 1f - flicker * 0.15f;
					lightrgbs[1] *= 1f - flicker * 0.15f;
					lightrgbs[2] *= 1f - flicker * 0.15f;
					prog.RgbaGlowIn = new Vec4f(lightrgbs[0], lightrgbs[1], lightrgbs[2], 1);
				} else {
					prog.RgbaGlowIn = new Vec4f(0, 0, 0, 0);
				}
				prog.NormalShaded = 0;
				prog.RgbaLightIn = lightrgbs;
				int glow = 255 - (int)(flicker * 50);
				prog.ExtraGlow = isBurning ? glow : 0;

				// The coal or embers.
				rpi.BindTexture2d(isBurning ? emberTexPos.atlasTextureId : fuelsTexPos.atlasTextureId);
				prog.ModelMatrix = ModelMat.Identity().Translate(pos.X - camPos.X, pos.Y - camPos.Y + 0.1, pos.Z - camPos.Z).Values;
				prog.ViewMatrix = rpi.CameraMatrixOriginf;
				prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
				rpi.RenderMesh(isBurning ? emberQuadRef : burnsQuadRef);
			}
			prog.Stop();
		}

		public void Dispose() {
			api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
			emberQuadRef?.Dispose();
			burnsQuadRef?.Dispose();
			fuelsMeshRef?.Dispose();
		}
	}
}
