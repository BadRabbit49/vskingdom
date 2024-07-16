using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VSKingdom {
	public class InvSentryDialog : GuiDialog {
		public override bool DisableMouseGrab => false;
		public override bool PrefersUngrabbedMouse => false;
		public override bool UnregisterOnClose => true;
		public override double DrawOrder => 0.2;
		public override string ToggleKeyCombinationCode => null;

		protected const double Positions = 0.6;
		protected const double Alignment = 0.8;
		protected const double UIPadding = 3.0;
		protected const double HeightPad = 51.0;

		protected string langCodes;

		protected Vec3d entpos = new Vec3d();
		protected InventorySentry inventory;
		protected EntitySentry entity;
		protected EntityPlayer player;
		protected ITreeAttribute loyalties => entity.WatchedAttributes.GetTreeAttribute("loyalties");

		public InvSentryDialog(InventorySentry inventory, EntitySentry entity, ICoreClientAPI capi) : base(capi) {
			this.inventory = inventory;
			this.entity = entity;
			this.player = capi.World.Player.Entity;
			this.capi = capi;
			this.langCodes = (capi.World.Player as IServerPlayer)?.LanguageCode ?? "en";
			ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
			bgBounds.BothSizing = ElementSizing.FitToChildren;
			// Dialog Bounds.
			ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle).WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0.0);
			// Inventory Bounds.
			ElementBounds armourSlotBoundsHead = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 020.0 + UIPadding, 1, 1).FixedGrow(0.0, UIPadding);
			ElementBounds armourSlotBoundsBody = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 122.0 + UIPadding, 1, 1).FixedGrow(0.0, UIPadding);
			ElementBounds armourSlotBoundsLegs = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 224.0 + UIPadding, 1, 1).FixedGrow(0.0, UIPadding);
			ElementBounds clothingsSlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 020.0 + UIPadding, 1, 6).FixedGrow(0.0, UIPadding);
			ElementBounds accessorySlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 020.0 + UIPadding, 1, 6).FixedGrow(0.0, UIPadding);
			ElementBounds rightSlotBoundsLHand = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 020.0 + UIPadding, 1, 1).FixedGrow(0.0, UIPadding);
			ElementBounds rightSlotBoundsRHand = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 071.0 + UIPadding, 1, 1).FixedGrow(0.0, UIPadding);
			ElementBounds backpackOnSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 122.0 + UIPadding, 1, 1).FixedGrow(0.0, UIPadding);
			ElementBounds munitionsSlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 173.0 + UIPadding, 1, 1).FixedGrow(0.0, UIPadding);
			ElementBounds healthitmSlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 224.0 + UIPadding, 1, 1).FixedGrow(0.0, UIPadding);
			// Command Bounds.
			ElementBounds gowanderButtonBounds = ElementStdBounds.ToggleButton(0.0, 326.0 + UIPadding, 102, HeightPad).FixedGrow(0.0, UIPadding);
			ElementBounds gofollowButtonBounds = ElementStdBounds.ToggleButton(0.0, 326.0 + UIPadding, 102, HeightPad).FixedGrow(0.0, UIPadding);
			ElementBounds goattackButtonBounds = ElementStdBounds.ToggleButton(0.0, 377.0 + UIPadding, 102, HeightPad).FixedGrow(0.0, UIPadding);
			ElementBounds gopursueButtonBounds = ElementStdBounds.ToggleButton(0.0, 377.0 + UIPadding, 102, HeightPad).FixedGrow(0.0, UIPadding);
			ElementBounds doshiftsButtonBounds = ElementStdBounds.ToggleButton(0.0, 428.0 + UIPadding, 102, HeightPad).FixedGrow(0.0, UIPadding);
			ElementBounds donightsButtonBounds = ElementStdBounds.ToggleButton(0.0, 428.0 + UIPadding, 102, HeightPad).FixedGrow(0.0, UIPadding);
			ElementBounds returntoButtonBounds = ElementStdBounds.ToggleButton(0.0, 479.0 + UIPadding, 102, HeightPad).FixedGrow(0.0, UIPadding);

			clothingsSlotsBounds.FixedRightOf(armourSlotBoundsHead, 10.0).FixedRightOf(armourSlotBoundsBody, 10.0).FixedRightOf(armourSlotBoundsLegs, 10.0);
			accessorySlotsBounds.FixedRightOf(clothingsSlotsBounds, 10.0);

			clothingsSlotsBounds.fixedHeight -= 6.0;
			accessorySlotsBounds.fixedHeight -= 6.0;

			backpackOnSlotBounds.FixedRightOf(accessorySlotsBounds, 10.0);
			rightSlotBoundsLHand.FixedRightOf(accessorySlotsBounds, 10.0);
			rightSlotBoundsRHand.FixedRightOf(accessorySlotsBounds, 10.0);
			munitionsSlotsBounds.FixedRightOf(accessorySlotsBounds, 10.0);
			healthitmSlotsBounds.FixedRightOf(accessorySlotsBounds, 10.0);

			gofollowButtonBounds.FixedRightOf(gowanderButtonBounds, 10.0);
			gopursueButtonBounds.FixedRightOf(goattackButtonBounds, 10.0);
			donightsButtonBounds.FixedRightOf(doshiftsButtonBounds, 10.0);

			SingleComposer = capi.Gui.CreateCompo("sentrycontents" + entity.EntityId, dialogBounds)
				.AddShadedDialogBG(bgBounds)
				.AddDialogTitleBar(GetName(), onClose: OnCloseDialog);
			SingleComposer
				.BeginChildElements(bgBounds);
			SingleComposer
				.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.HeadArmorSlotId }, armourSlotBoundsHead, "armorSlotsHead")
				.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.BodyArmorSlotId }, armourSlotBoundsBody, "armorSlotsBody")
				.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.LegsArmorSlotId }, armourSlotBoundsLegs, "armorSlotsLegs");
			SingleComposer
				.AddItemSlotGrid(inventory, SendInvPacket, 1, InventorySentry.ClothingsSlotIds, clothingsSlotsBounds, "clothingsSlots")
				.AddItemSlotGrid(inventory, SendInvPacket, 1, InventorySentry.AccessorySlotIds, accessorySlotsBounds, "accessorySlots");
			SingleComposer
				.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.LHandItemSlotId }, rightSlotBoundsLHand, "otherSlotsLHnd")
				.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.RHandItemSlotId }, rightSlotBoundsRHand, "otherSlotsRHnd")
				.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.BPackItemSlotId }, backpackOnSlotBounds, "otherSlotsPack")
				.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.FoodsItemSlotId }, munitionsSlotsBounds, "otherSlotsAmmo")
				.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.HealthItmSlotId }, healthitmSlotsBounds, "otherSlotsHeal");
			SingleComposer
				.AddButton(LangUtility.GetL(langCodes, "entries-keyword-wander"), () => OnGiveCommand("command_wander"), gowanderButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersWander")
				.AddButton(LangUtility.GetL(langCodes, "entries-keyword-follow"), () => OnGiveCommand("command_follow"), gofollowButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersFollow")
				.AddButton(LangUtility.GetL(langCodes, "entries-keyword-firing"), () => OnGiveCommand("command_firing"), goattackButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersAttack")
				.AddButton(LangUtility.GetL(langCodes, "entries-keyword-pursue"), () => OnGiveCommand("command_pursue"), gopursueButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersPursue")
				.AddButton(LangUtility.GetL(langCodes, "entries-keyword-shifts"), () => OnGiveCommand("command_shifts"), doshiftsButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersShifts")
				.AddButton(LangUtility.GetL(langCodes, "entries-keyword-nights"), () => OnGiveCommand("command_nights"), donightsButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersNights")
				.AddButton(LangUtility.GetL(langCodes, "entries-keyword-return"), () => OnGiveCommand("command_return"), returntoButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersReturn")
			.EndChildElements();
			SingleComposer.Compose();
		}

		public override void OnFinalizeFrame(float dt) {
			base.OnFinalizeFrame(dt);
			entpos = entity.Pos.XYZ.Clone();
			entpos.Add(entity.SelectionBox.X2 - entity.OriginSelectionBox.X2, 0.0, entity.SelectionBox.Z2 - entity.OriginSelectionBox.Z2);
			if (!InRange()) {
				capi.Event.EnqueueMainThreadTask(delegate { TryClose(); }, "closedsentryinvlog");
			}
		}
		
		public override void OnGuiClosed() {
			base.OnGuiClosed();
			capi.Network.SendPacketClient(capi.World.Player.InventoryManager.CloseInventory(inventory));
			SingleComposer.GetSlotGrid("armorSlotsHead")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("armorSlotsBody")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("armorSlotsLegs")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("clothingsSlots")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("accessorySlots")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("otherSlotsLHnd")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("otherSlotsRHnd")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("otherSlotsPack")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("otherSlotsAmmo")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("otherSlotsHeal")?.OnGuiClosed(capi);
		}

		public override void OnRenderGUI(float deltaTime) {
			if (capi.Settings.Bool["immersiveMouseMode"]) {
				double offX = entity.SelectionBox.X2 - entity.OriginSelectionBox.X2;
				double offZ = entity.SelectionBox.Z2 - entity.OriginSelectionBox.Z2;
				Vec3d pos = MatrixToolsd.Project(new Vec3d(entity.Pos.X + offX, entity.Pos.Y + Positions, entity.Pos.Z + offZ), capi.Render.PerspectiveProjectionMat, capi.Render.PerspectiveViewMat, capi.Render.FrameWidth, capi.Render.FrameHeight);
				if (pos.Z < 0.0) {
					return;
				}
				SingleComposer.Bounds.Alignment = EnumDialogArea.None;
				SingleComposer.Bounds.fixedOffsetX = 0.0;
				SingleComposer.Bounds.fixedOffsetY = 0.0;
				SingleComposer.Bounds.absFixedX = pos.X - SingleComposer.Bounds.OuterWidth / 2.0;
				SingleComposer.Bounds.absFixedY = capi.Render.FrameHeight - pos.Y - SingleComposer.Bounds.OuterHeight * Alignment;
				SingleComposer.Bounds.absMarginX = 0.0;
				SingleComposer.Bounds.absMarginY = 0.0;
			}
			base.OnRenderGUI(deltaTime);
		}

		protected bool OnGiveCommand(string orders) {
			// This is a brute-force way of doing this. I didn't want it to come to this but here it is.
			entity.WatchedAttributes.GetTreeAttribute("loyalties").SetBool(orders, !loyalties.GetBool(orders));
			SentryOrders newOrders = new SentryOrders();
			newOrders.entityUID = entity.EntityId;
			switch (orders) {
				case "command_wander": newOrders.wandering = !loyalties.GetBool(orders); break;
				case "command_follow": newOrders.following = !loyalties.GetBool(orders); break;
				case "command_firing": newOrders.attacking = !loyalties.GetBool(orders); break;
				case "command_pursue": newOrders.pursueing = !loyalties.GetBool(orders); break;
				case "command_shifts": newOrders.shifttime = !loyalties.GetBool(orders); break;
				case "command_nights": newOrders.nighttime = !loyalties.GetBool(orders); break;
				case "command_return": newOrders.returning = !loyalties.GetBool(orders); break;
			}

			(entity.Api as ICoreServerAPI)?.Network.GetChannel("sentrynetwork").SendPacket<SentryOrders>(newOrders, player as IServerPlayer);
			capi.ShowChatMessage(LangUtility.GetL(langCodes, $"entries-keyword-{orders.Replace("command_", "")}-{loyalties.GetBool(orders).ToString().ToLower()}"));
			TryClose();
			if (entity.ruleOrder[0]) {
				entity.WatchedAttributes.SetFloat("wanderRangeMul", 2f);
			} else {
				entity.WatchedAttributes.SetFloat("wanderRangeMul", 1f);
			}
			if (entity.ruleOrder[1]) {
				entity.WatchedAttributes.SetString("guardedPlayerUid", player.PlayerUID);
				entity.WatchedAttributes.SetLong("guardedEntityId", player.EntityId);
			}
			return true;
		}

		protected void OnCloseDialog() {
			TryClose();
		}

		protected void SendInvPacket(object packet) {
			capi.Network.SendPacketClient(packet);
		}

		private bool InRange() {
			return player.Pos.XYZ.Add(player.LocalEyePos).SquareDistanceTo(entity.Pos) < Math.Pow(player.Player.WorldData.PickingRange, 2);
		}

		private string GetName() {
			return entity.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name") ?? Lang.GetL(langCodes, entity.Code.ToString());
		}
	}
}