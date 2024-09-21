using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VSKingdom {
	public interface IModData { }
	public static class VSKingdomData {
		public static T ReadData<T>(ICoreAPI api, string jsonData) where T : class, IModData {
			T config;
			try {
				config = LoadData<T>(api, jsonData);
				if (config == null) {
					SaveData<T>(api, jsonData);
					config = LoadData<T>(api, jsonData);
				} else {
					SaveData(api, jsonData, config);
				}
			} catch {
				SaveData<T>(api, jsonData);
				config = LoadData<T>(api, jsonData);
			}
			return config;
		}

		public static T LoadData<T>(ICoreAPI api, string jsonData) where T : IModData {
			return api.LoadModConfig<T>(jsonData);
		}

		public static T CopyData<T>(ICoreAPI api, T data = null) where T : class, IModData {
			return (T)Activator.CreateInstance(typeof(T), new object[] { api, data });
		}

		public static void SaveData<T>(ICoreAPI api, string jsonData, T previousData = null) where T : class, IModData {
			api.StoreModConfig(CopyData<T>(api, previousData), jsonData);
		}
	}

	public class KingdomData : IModData {
		public readonly string Comments = "IMPORTANT: Do not change GUIDs or you will die!";
		public Dictionary<string, Kingdom> Kingdoms { get; set; } = new();

		public KingdomData(ICoreAPI api, KingdomData previousData = null) {
			if (previousData != null) {
				foreach ((string guid, Kingdom kingdom) in previousData.Kingdoms) {
					if (!Kingdoms.ContainsKey(guid)) {
						Kingdoms.Add(guid, kingdom);
					}
				}
			}
			if (api != null && api is ICoreServerAPI sapi) {
				GetKingdoms(sapi);
			}
		}

		private void GetKingdoms(ICoreServerAPI sapi) {
			byte[] kingdomData = sapi.WorldManager.SaveGame.GetData("kingdomData");
			List<Kingdom> kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
			foreach (Kingdom kingdom in kingdomList) {
				if (Kingdoms.ContainsKey(kingdom.KingdomGUID)) {
					continue;
				}
				Kingdoms.Add(kingdom.KingdomGUID, kingdom);
			}
		}
	}

	public class CultureData : IModData {
		public readonly string Comments = "IMPORTANT: Do not change GUIDs or you will die!";
		public Dictionary<string, Culture> Cultures { get; set; } = new();

		public CultureData(ICoreAPI api, CultureData previousData = null) {
			if (previousData != null) {
				foreach ((string guid, Culture culture) in previousData.Cultures) {
					if (!Cultures.ContainsKey(guid)) {
						Cultures.Add(guid, culture);
					}
				}
			}
			if (api != null && api is ICoreServerAPI sapi) {
				GetCultures(sapi);
			}
		}

		private void GetCultures(ICoreServerAPI sapi) {
			byte[] cultureData = sapi.WorldManager.SaveGame.GetData("cultureData");
			List<Culture> cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
			foreach (Culture culture in cultureList) {
				if (Cultures.ContainsKey(culture.CultureGUID)) {
					continue;
				}
				Cultures.Add(culture.CultureGUID, culture);
			}
		}
	}
}