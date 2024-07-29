using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace VSKingdom {
	public class EntityBehaviorFullNames : EntityBehavior {
		public EntityBehaviorFullNames(Entity entity) : base(entity) {
			ITreeAttribute nametagTree = entity.WatchedAttributes.GetTreeAttribute("nametag");
			if (nametagTree == null) {
				entity.WatchedAttributes.SetAttribute("nametag", nametagTree = new TreeAttribute());
				nametagTree.SetString("name", "");
				nametagTree.SetString("last", "");
				nametagTree.SetInt("showtagonlywhentargeted", 0);
				nametagTree.SetInt("renderRange", 500);
				entity.WatchedAttributes.MarkPathDirty("nametag");
			}
		}

		public bool ShowOnlyWhenTargeted {
			get => entity.WatchedAttributes.GetTreeAttribute("nametag")?.GetBool("showtagonlywhentargeted") == true;
			set => entity.WatchedAttributes.GetTreeAttribute("nametag")?.SetBool("showtagonlywhentargeted", value);
		}

		public int RenderRange {
			get => entity.WatchedAttributes.GetTreeAttribute("nametag").GetInt("renderRange");
			set => entity.WatchedAttributes.GetTreeAttribute("nametag")?.SetInt("renderRange", value);
		}

		public string CommonName {
			get => entity.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name");
			set => entity.WatchedAttributes.GetTreeAttribute("nametag")?.SetString("name", value);
		}

		public string FamilyName {
			get => entity.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("last");
			set => entity.WatchedAttributes.GetTreeAttribute("nametag")?.SetString("last", value);
		}

		public override string PropertyName() {
			return "KingdomFullNames";
		}

		public override void Initialize(EntityProperties entityType, JsonObject attributes) {
			base.Initialize(entityType, attributes);
			// If a name already exists use that instead, otherwise try generating a random default one from the config list.
			if (CommonName != null && CommonName != "" && CommonName.Length > 0 && FamilyName != null && FamilyName != "" && FamilyName.Length > 0) {
				SetName(CommonName, FamilyName);
			} else if (entity.Api.Side == EnumAppSide.Server) {
				string[] commonNameList = new string[] { };
				string[] familyNameList = LangUtility.Open(entity.Api.World.Config.GetAsString("BasicLastNames"));
				switch (entity.Code.EndVariant()) {
					case "masc": commonNameList = LangUtility.Open(entity.Api.World.Config.GetAsString("BasicMascNames")); break;
					case "femm": commonNameList = LangUtility.Open(entity.Api.World.Config.GetAsString("BasicFemmNames")); break;
					default: commonNameList = LangUtility.Fuse(LangUtility.Open(entity.Api.World.Config.GetAsString("BasicMascNames")), LangUtility.Open(entity.Api.World.Config.GetAsString("BasicFemmNames"))); break;
				}
				Random rnd = new Random();
				SetName(commonNameList[rnd.Next(0, commonNameList.Length - 1)], familyNameList[rnd.Next(0, familyNameList.Length - 1)]);
			}
			RenderRange = attributes["renderRange"].AsInt(500);
			ShowOnlyWhenTargeted = attributes["showtagonlywhentargeted"].AsBool();
		}

		public override void OnEntitySpawn() {
			base.OnEntitySpawn();
		}

		public void SetName(string commonName, string familyName = null) {
			ITreeAttribute nametagTree = entity.WatchedAttributes.GetOrAddTreeAttribute("nametag");
			nametagTree.SetString("name", commonName);
			nametagTree.SetString("last", familyName);
			entity.WatchedAttributes.MarkPathDirty("nametag");
		}
	}
}