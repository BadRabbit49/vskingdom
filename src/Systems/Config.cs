using Vintagestory.API.Common;

namespace VSKingdom {
	class KingdomConfig {
		private VSKingdomConfig config;

		private VSKingdomConfig LoadConfig(ICoreAPI api) {
			return api.LoadModConfig<VSKingdomConfig>("VSKingdomConfig.json");
		}

		private void MakeConfig(ICoreAPI api) {
			api.StoreModConfig<VSKingdomConfig>(new VSKingdomConfig(api.Assets.TryGet(AssetLocation.Create(DefaultSettings)).ToObject<VSKingdomConfig>()), "VSKingdomConfig.json");
		}

		private void MakeConfig(ICoreAPI api, VSKingdomConfig prevSettings) {
			api.StoreModConfig<VSKingdomConfig>(new VSKingdomConfig(prevSettings), "VSKingdomConfig.json");
		}

		public void ReadConfig(ICoreAPI api) {
			try {
				config = LoadConfig(api);
				// Only generate new files to default when config is null.
				if (config is null) {
					MakeConfig(api);
					config = LoadConfig(api);
				} else {
					MakeConfig(api, config);
				}
			} catch {
				MakeConfig(api);
				config = LoadConfig(api);
			}
			api.World.Config.SetBool("Allowed_SentryTeamHurt", config.Allowed_SentryTeamHurt);
			api.World.Config.SetBool("Allowed_SentryTripping", config.Allowed_SentryTripping);
			api.World.Config.SetBool("Allowed_SentryDrowning", config.Allowed_SentryDrowning);
			api.World.Config.SetBool("Allowed_SentryTeleport", config.Allowed_SentryTeleport);
			api.World.Config.SetBool("Allowed_SentryDropLoot", config.Allowed_SentryDropLoot);
			api.World.Config.SetBool("Allowed_InfiniteArrows", config.Allowed_InfiniteArrows);
			api.World.Config.SetBool("Allowed_PlayerDropLoot", config.Allowed_PlayerDropLoot);
			api.World.Config.SetBool("Allowed_StartCivilWars", config.Allowed_StartCivilWars);
			api.World.Config.SetBool("Limited_SentryResupply", config.Limited_SentryResupply);
			api.World.Config.SetBool("Limited_SentryReviving", config.Limited_SentryReviving);
			api.World.Config.SetBool("Nametag_PlayerNametags", config.Nametag_PlayerNametags);
			api.World.Config.SetBool("Nametag_SentryNametags", config.Nametag_SentryNametags);
			api.World.Config.SetLong("Nametag_RenderDistance", config.Nametag_RenderDistance);
			api.World.Config.SetLong("Culture_MinCreateLevel", config.Culture_MinCreateLevel);
			api.World.Config.SetLong("Culture_CreateCooldown", config.Culture_CreateCooldown);
			api.World.Config.SetLong("Culture_MaxUserCreated", config.Culture_MaxUserCreated);
			api.World.Config.SetLong("Kingdom_MinCreateLevel", config.Kingdom_MinCreateLevel);
			api.World.Config.SetLong("Kingdom_CreateCooldown", config.Kingdom_CreateCooldown);
			api.World.Config.SetLong("Kingdom_MaxUserCreated", config.Kingdom_MaxUserCreated);
		}
	}

	class VSKingdomConfig {
		public VSKingdomConfig() { }
		// Default Settings for Configuration.
		public bool Allowed_SentryTeamHurt { get; set; } = true;
		public bool Allowed_SentryTripping { get; set; } = true;
		public bool Allowed_SentryDrowning { get; set; } = true;
		public bool Allowed_SentryTeleport { get; set; } = true;
		public bool Allowed_SentryDropLoot { get; set; } = true;
		public bool Allowed_InfiniteArrows { get; set; } = true;
		public bool Allowed_PlayerDropLoot { get; set; } = true;
		public bool Allowed_StartCivilWars { get; set; } = true;
		public bool Limited_SentryResupply { get; set; } = true;
		public bool Limited_SentryReviving { get; set; } = true;
		public bool Nametag_PlayerNametags { get; set; } = true;
		public bool Nametag_SentryNametags { get; set; } = true;
		public long Nametag_RenderDistance { get; set; } = 500;
		public long Culture_MinCreateLevel { get; set; } = -1;
		public long Culture_CreateCooldown { get; set; } = -1;
		public long Culture_MaxUserCreated { get; set; } = -1;
		public long Kingdom_MinCreateLevel { get; set; } = -1;
		public long Kingdom_CreateCooldown { get; set; } = -1;
		public long Kingdom_MaxUserCreated { get; set; } = -1;
		// Loaded Previous Configuration if exists.
		public VSKingdomConfig(VSKingdomConfig prev) {
			Allowed_SentryTeamHurt = prev.Allowed_SentryTeamHurt;
			Allowed_SentryTripping = prev.Allowed_SentryTripping;
			Allowed_SentryDrowning = prev.Allowed_SentryDrowning;
			Allowed_SentryTeleport = prev.Allowed_SentryTeleport;
			Allowed_SentryDropLoot = prev.Allowed_SentryDropLoot;
			Allowed_PlayerDropLoot = prev.Allowed_PlayerDropLoot;
			Allowed_StartCivilWars = prev.Allowed_StartCivilWars;
			Limited_SentryResupply = prev.Limited_SentryResupply;
			Limited_SentryReviving = prev.Limited_SentryReviving;
			Nametag_PlayerNametags = prev.Nametag_PlayerNametags;
			Nametag_SentryNametags = prev.Nametag_SentryNametags;
			Nametag_RenderDistance = prev.Nametag_RenderDistance;
			Culture_MinCreateLevel = prev.Culture_MinCreateLevel;
			Culture_CreateCooldown = prev.Culture_CreateCooldown;
			Culture_MaxUserCreated = prev.Culture_MaxUserCreated;
			Kingdom_MinCreateLevel = prev.Kingdom_MinCreateLevel;
			Kingdom_CreateCooldown = prev.Kingdom_CreateCooldown;
			Kingdom_MaxUserCreated = prev.Kingdom_MaxUserCreated;
		}
	}
}