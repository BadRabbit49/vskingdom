using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace VSKingdom {
	public class EntityBehaviorFullNames : EntityBehavior {
		public EntityBehaviorFullNames(Entity entity) : base(entity) {
			ITreeAttribute nametagTree = entity.WatchedAttributes.GetTreeAttribute("nametag");
			if (nametagTree == null) {
				entity.WatchedAttributes.SetAttribute("nametag", nametagTree = new TreeAttribute());
				nametagTree.SetString("name", "");
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
		
		public string DisplayName {
			get => entity.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name");
		}

		public override string PropertyName() {
			return "KingdomFullNames";
		}

		public override void Initialize(EntityProperties entityType, JsonObject attributes) {
			base.Initialize(entityType, attributes);
			// Create a random new name with the entityType.
			if ((DisplayName == null || DisplayName.Length == 0) && attributes["selectFromRandomName"].Exists) {
				string[] randomName = attributes["selectFromRandomName"].AsArray<string>();
				SetName(randomName[entity.World.Rand.Next(randomName.Length)]);
			}
			// Grab a random name from the culture they are part of.
			if ((DisplayName == null || DisplayName.Length == 0) && entity.HasBehavior<EntityBehaviorLoyalties>()) {
				if (entity.Code.EndVariant() == "masc") {
					string[] mascNames = entity.GetBehavior<EntityBehaviorLoyalties>()?.cachedCulture.MascFNames;
					SetName(mascNames[entity.World.Rand.Next(mascNames.Length)]);
				}
				if (entity.Code.EndVariant() == "femm") {
					string[] femmNames = entity.GetBehavior<EntityBehaviorLoyalties>()?.cachedCulture.FemmFNames;
					SetName(femmNames[entity.World.Rand.Next(femmNames.Length)]);
				}
			}
			RenderRange = attributes["renderRange"].AsInt(700);
			ShowOnlyWhenTargeted = attributes["showtagonlywhentargeted"].AsBool(true);
		}

		public override void OnEntitySpawn() {
			base.OnEntitySpawn();
		}

		public void SetName(string playername) {
			ITreeAttribute nametagTree = entity.WatchedAttributes.GetTreeAttribute("nametag");
			if (nametagTree == null) {
				entity.WatchedAttributes.SetAttribute("nametag", nametagTree = new TreeAttribute());
			}
			nametagTree.SetString("name", playername);
			entity.WatchedAttributes.MarkPathDirty("nametag");
		}
	}
}