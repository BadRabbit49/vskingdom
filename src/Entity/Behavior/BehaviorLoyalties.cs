using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.API.Config;

namespace VSKingdom {
	public class EntityBehaviorLoyalties : EntityBehavior {
		public EntityBehaviorLoyalties(Entity entity) : base(entity) { }

		public ITreeAttribute loyalties {
			get {
				return entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties");
			}
			set {
				entity.WatchedAttributes.SetAttribute("loyalties", value);
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		public EnlistedStatus enlistedStatus {
			get {
				EnlistedStatus level;
				if (Enum.TryParse<EnlistedStatus>(loyalties.GetString("enlistedStatus"), out level)) {
					return level;
				}
				return EnlistedStatus.CIVILIAN;
			}
			set {
				loyalties.SetString("enlistedStatus", value.ToString());
				entity.WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		public string kingdomGUID {
			get => loyalties.GetString("kingdom_guid");
			set => loyalties.SetString("kingdom_guid", value);
		}

		public string cultureGUID {
			get => loyalties.GetString("culture_guid");
			set => loyalties.SetString("culture_guid", value);
		}

		public string leadersGUID {
			get => loyalties.GetString("leaders_guid");
			set => loyalties.SetString("leaders_guid", value);
		}

		public double outpostSIZE {
			get => loyalties.GetDouble("outpost_size");
			set => loyalties.SetDouble("outpost_size", value);
		}

		public BlockPos outpostXYZD {
			get => loyalties.GetBlockPos("outpost_xyzd");
			set => loyalties.SetBlockPos("outpost_xyzd", value);
		}

		public bool commandWANDER {
			get => loyalties.GetBool("command_wander");
			set => loyalties.SetBool("command_wander", value);
		}

		public bool commandFOLLOW {
			get => loyalties.GetBool("command_follow");
			set => loyalties.SetBool("command_follow", value);
		}

		public bool commandFIRING {
			get => loyalties.GetBool("command_firing");
			set => loyalties.SetBool("command_firing", value);
		}

		public bool commandPURSUE {
			get => loyalties.GetBool("command_pursue");
			set => loyalties.SetBool("command_pursue", value);
		}

		public bool commandSHIFTS {
			get => loyalties.GetBool("command_shifts");
			set => loyalties.SetBool("command_shifts", value);
		}

		public bool commandNIGHTS {
			get => loyalties.GetBool("command_nights");
			set => loyalties.SetBool("command_nights", value);
		}

		public bool commandRETURN {
			get => loyalties.GetBool("command_return");
			set => loyalties.SetBool("command_return", value);
		}

		public override string PropertyName() {
			return "KingdomLoyalties";
		}

		public override void AfterInitialized(bool onFirstSpawn) {
			base.AfterInitialized(onFirstSpawn);
			if (entity is EntitySentry sentry) {
				if (kingdomGUID is null) {
					kingdomGUID = sentry.baseGroup;
				}
				if (cultureGUID is null) {
					cultureGUID = "00000000";
				}
				if (outpostXYZD is null) {
					outpostXYZD = entity.ServerPos.AsBlockPos;
				}
				commandWANDER = true;
				commandFOLLOW = false;
				commandFIRING = true;
				commandPURSUE = true;
				commandSHIFTS = false;
				commandNIGHTS = false;
				commandRETURN = false;
			}
		}

		public override void OnEntitySpawn() {
			base.OnEntitySpawn();
			if (entity is EntityPlayer) {
				if (kingdomGUID is null) {
					kingdomGUID = "00000000";
				}
				if (cultureGUID is null) {
					cultureGUID = "00000000";
				}
				loyalties.RemoveAttribute("leaders_name");
				loyalties.RemoveAttribute("leaders_guid");
			}
		}

		public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled) {
			// Other players won't be interactable for now.
			if (byEntity is EntityPlayer player && mode == EnumInteractMode.Interact && entity is not EntityPlayer) {
				// Remind them to join their leaders kingdom if they aren't already in it.
				if (leadersGUID == player.PlayerUID && kingdomGUID != player.GetBehavior<EntityBehaviorLoyalties>()?.kingdomGUID) {
					kingdomGUID = byEntity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") ?? kingdomGUID;
				}
				// While a STRANGER has something in their ACTIVE SLOT try commands.
				if (enlistedStatus == EnlistedStatus.CIVILIAN && entity.Alive && leadersGUID == null && itemslot.Itemstack != null && player.Controls.Sneak) {
					TryRecruiting(itemslot, player.Player as IServerPlayer);
					return;
				}
				// Try to revive if the entity is dead but not a carcass.
				if (!entity.Alive && itemslot.Itemstack != null && byEntity.Controls.Sneak) {
					TryReviveWith(itemslot);
					return;
				}
				// While the OWNER or a GROUPMEMBER has something in their ACTIVE SLOT try commands.
				if (entity.Alive && itemslot.Itemstack != null && byEntity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") == kingdomGUID) {
					TryOrderRally(itemslot, player.Player);
					return;
				}
				// While the OWNER is SNEAKING with an EMPTY SLOT open inventory dialogbox.
				if (leadersGUID == player.PlayerUID && player.Controls.Sneak && itemslot.Empty) {
					(entity as EntitySentry).ToggleInventoryDialog(player.Player);
				}
			} else {
				base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
			}
		}

		public override void GetInfoText(StringBuilder infotext) {
			// Place Kingdom (if any), Leader (if any) and Guardpost (if any), here.
			if (kingdomGUID is not null) {
				//infotext.AppendLine(string.Concat(LangUtility.Get("gui-kingdom-name"), DataUtility.GetKingdomName(kingdomGUID)));
				infotext.AppendLine(string.Concat(Lang.Get("vskingdom:gui-kingdom-guid"), kingdomGUID));
			}
			if (leadersGUID is not null) {
				//infotext.AppendLine(string.Concat(LangUtility.Get("gui-leaders-name"), DataUtility.GetAPlayer(leadersGUID).PlayerName));
				infotext.AppendLine(string.Concat(Lang.Get("vskingdom:gui-leaders-guid"), leadersGUID));
			}
			if (outpostXYZD is not null) {
				infotext.AppendLine(string.Concat(Lang.Get("vskingdom:gui-outpost-xyzd"), outpostXYZD.ToString()));
			}
			base.GetInfoText(infotext);
		}

		private void TryRecruiting(ItemSlot itemslot, IServerPlayer player) {
			// If the entity isn't already owned, giving it some kind of currency will hire it on to join.
			if (itemslot.Itemstack.ItemAttributes["currency"].Exists) {
				itemslot.TakeOut(1);
				itemslot.MarkDirty();
				// Set the owner to this player, and set the enlistment to ENLISTED.
				enlistedStatus = EnlistedStatus.ENLISTED;
				// If the owner also is in a group then go ahead and join that too.
				string newKingdomGUID = player.Entity.GetBehavior<EntityBehaviorLoyalties>()?.kingdomGUID ?? loyalties.GetString("kingdom_guid");
				(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.GetTreeAttribute("loyalties").SetString("kingdom_guid", newKingdomGUID);
				(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.MarkPathDirty("loyalties");
			}
		}

		private void TryReviveWith(ItemSlot itemslot) {
			if (itemslot.Itemstack.Item is ItemPoultice) {
				float healAmount = itemslot.Itemstack.ItemAttributes["health"].AsFloat();
				entity.Revive();
				(entity.Api as ICoreServerAPI)?.World.GetEntityById(entity.EntityId).WatchedAttributes.GetTreeAttribute("health").SetFloat("currenthealth", healAmount);
				itemslot.TakeOut(1);
				itemslot.MarkDirty();
			}
		}

		private void TryOrderRally(ItemSlot itemslot, IPlayer player) {
			if (itemslot.Itemstack.Item is ItemBanner) {
				EntityPlayer playerEnt = player.Entity;
				string playerKingdomID = playerEnt.WatchedAttributes.GetTreeAttribute("loyalties").GetString("kingdom_guid");
				// Activate orders for all surrounding soldiers of this player's faction to follow them!
				foreach (Entity soldier in entity.World.GetEntitiesAround(entity.ServerPos.XYZ, 15, 4, entity => (entity is EntitySentry sentry && sentry.kingdomID == playerKingdomID))) {
					var taskManager = soldier.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
					taskManager.ExecuteTask<AiTaskSentryFollow>();
				}
			}
		}
	}
}