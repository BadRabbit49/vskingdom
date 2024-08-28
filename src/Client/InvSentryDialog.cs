using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class InvSentryDialog : GuiDialog {
		public override bool DisableMouseGrab => false;
		public override bool PrefersUngrabbedMouse => false;
		public override bool UnregisterOnClose => true;
		public override double DrawOrder => 0.2;
		public override string ToggleKeyCombinationCode => null;

		protected const double Positions = 0.6;
		protected const double Alignment = 0.8;
		protected const double UIPadding = 3;
		protected const double WidthsPad = 103;
		protected const double HeightPad = 51;
		protected const double Quartered = 25;

		protected string langCodes;

		protected Vec3d entpos = new Vec3d();
		protected InventorySentry inventory;
		protected EntitySentry entity;
		protected EntityPlayer player;

		public InvSentryDialog(InventorySentry inventory, EntitySentry entity, ICoreClientAPI capi) : base(capi) {
			this.inventory = inventory;
			this.entity = entity;
			this.player = capi.World.Player.Entity;
			this.langCodes = (player as IServerPlayer)?.LanguageCode ?? "en";
			this.capi = capi;
			bool playerIsLeader = entity.WatchedAttributes.GetString("leadersGUID") == player.PlayerUID;
			bool playerIsFriend = entity.WatchedAttributes.GetString("kingdomGUID") == player.WatchedAttributes.GetString("kingdomGUID");
			bool entityIsSmited = entity.Alive == false;
			double sliderLength = 192;

			ElementBounds invBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
			invBounds.BothSizing = ElementSizing.FitToChildren;
			// Dialog Bounds //
			ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle).WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);
			// Inventory Bounds //
			ElementBounds armourSlotBoundsHead = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 023, 1, 1).FixedGrow(0, UIPadding);
			ElementBounds armourSlotBoundsBody = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 125, 1, 1).FixedGrow(0, UIPadding);
			ElementBounds armourSlotBoundsLegs = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 227, 1, 1).FixedGrow(0, UIPadding);
			ElementBounds clothingsSlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 023, 1, 6).FixedGrow(0, UIPadding);
			ElementBounds accessorySlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 023, 1, 6).FixedGrow(0, UIPadding);
			ElementBounds rightSlotBoundsLHand = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 023, 1, 1).FixedGrow(0, UIPadding);
			ElementBounds rightSlotBoundsRHand = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 074, 1, 1).FixedGrow(0, UIPadding);
			ElementBounds backpackOnSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 125, 1, 1).FixedGrow(0, UIPadding);
			ElementBounds munitionsSlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 176, 1, 1).FixedGrow(0, UIPadding);
			ElementBounds healthitmSlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 227, 1, 1).FixedGrow(0, UIPadding);
			// Orders Bounds //
			ElementBounds goWanderStringBounds = ElementBounds.Fixed(0, 330 + UIPadding, 76, 28);
			ElementBounds goWanderSliderBounds = ElementBounds.Fixed(0, 330 + Quartered, sliderLength, 30);
			ElementBounds goWanderSwitchBounds = ElementBounds.Fixed(sliderLength + 12, 330 + Quartered, 24, 24);
			ElementBounds goFollowStringBounds = ElementBounds.Fixed(0, 380 + UIPadding, 76, 24);
			ElementBounds goFollowSliderBounds = ElementBounds.Fixed(0, 380 + Quartered, sliderLength, 30);
			ElementBounds goFollowSwitchBounds = ElementBounds.Fixed(sliderLength + 12, 380 + Quartered, 24, 24);
			ElementBounds goEngageStringBounds = ElementBounds.Fixed(0, 430 + UIPadding, 76, 24);
			ElementBounds goEngageSliderBounds = ElementBounds.Fixed(0, 430 + Quartered, sliderLength, 30);
			ElementBounds goEngageSwitchBounds = ElementBounds.Fixed(sliderLength + 12, 430 + Quartered, 24, 24);
			ElementBounds goPursueStringBounds = ElementBounds.Fixed(0, 480 + UIPadding, 76, 24);
			ElementBounds goPursueSliderBounds = ElementBounds.Fixed(0, 480 + Quartered, sliderLength, 30);
			ElementBounds goPursueSwitchBounds = ElementBounds.Fixed(sliderLength + 12, 480 + Quartered, 24, 24);
			ElementBounds doShiftsStringBounds = ElementBounds.Fixed(0, 530 + UIPadding, 76, 24);
			ElementBounds doShiftsSliderBounds = ElementBounds.Fixed(0, 530 + Quartered, sliderLength/2, 30);
			ElementBounds doFinishSliderBounds = ElementBounds.Fixed(sliderLength/2, 530 + Quartered, sliderLength/2, 30);
			ElementBounds doShiftsSwitchBounds = ElementBounds.Fixed(sliderLength + 12, 530 + Quartered, 24, 24);
			ElementBounds goPatrolStringBounds = ElementBounds.Fixed(0, 580 + UIPadding, 76, 24);
			ElementBounds goPatrolInputsBounds = ElementBounds.Fixed(0, 580 + Quartered, sliderLength, 30);
			ElementBounds goPatrolSwitchBounds = ElementBounds.Fixed(sliderLength + 12, 580 + Quartered, 24, 24);
			ElementBounds returnToButtonBounds = ElementStdBounds.ToggleButton(0, 640 + UIPadding, 94, 48).WithAlignment(EnumDialogArea.RightFixed);
			// Inventory //
			clothingsSlotsBounds.FixedRightOf(armourSlotBoundsHead, 10);
			accessorySlotsBounds.FixedRightOf(clothingsSlotsBounds, 10);
			clothingsSlotsBounds.fixedHeight -= 6;
			accessorySlotsBounds.fixedHeight -= 6;
			backpackOnSlotBounds.FixedRightOf(accessorySlotsBounds, 10);
			rightSlotBoundsLHand.FixedRightOf(accessorySlotsBounds, 10);
			rightSlotBoundsRHand.FixedRightOf(accessorySlotsBounds, 10);
			munitionsSlotsBounds.FixedRightOf(accessorySlotsBounds, 10);
			healthitmSlotsBounds.FixedRightOf(accessorySlotsBounds, 10);

			var textsFont = CairoFont.WhiteSmallishText();
			textsFont.Orientation = EnumTextOrientation.Center;
			var titleFont = CairoFont.WhiteSmallishText();
			titleFont.Orientation = EnumTextOrientation.Justify;
			titleFont.WithFontSize(14);
			var typedFont = CairoFont.WhiteSmallishText();
			typedFont.Orientation = EnumTextOrientation.Left;
			typedFont.WithFontSize(10);

			SingleComposer = capi.Gui
			.CreateCompo("sentrycontents" + entity.EntityId, dialogBounds)
			.AddShadedDialogBG(invBounds)
			.AddDialogTitleBar(GetName(), OnCloseDialog, textsFont);
			SingleComposer
			.BeginChildElements(invBounds)
				.AddIf(playerIsLeader || entityIsSmited)
					.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.HeadArmorSlotId }, armourSlotBoundsHead, "armorSlotsHead")
					.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.BodyArmorSlotId }, armourSlotBoundsBody, "armorSlotsBody")
					.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.LegsArmorSlotId }, armourSlotBoundsLegs, "armorSlotsLegs")
					.AddItemSlotGrid(inventory, SendInvPacket, 1, InventorySentry.ClothingsSlotIds, clothingsSlotsBounds, "clothingsSlots")
					.AddItemSlotGrid(inventory, SendInvPacket, 1, InventorySentry.AccessorySlotIds, accessorySlotsBounds, "accessorySlots")
					.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.LHandItemSlotId }, rightSlotBoundsLHand, "otherSlotsLHnd")
					.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.RHandItemSlotId }, rightSlotBoundsRHand, "otherSlotsRHnd")
					.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.BPackItemSlotId }, backpackOnSlotBounds, "otherSlotsPack")
					.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.FoodsItemSlotId }, munitionsSlotsBounds, "otherSlotsAmmo")
					.AddItemSlotGrid(inventory, SendInvPacket, 1, new int[1] { InventorySentry.HealthItmSlotId }, healthitmSlotsBounds, "otherSlotsHeal")
				.EndIf()
				.AddIf(playerIsLeader)
					.AddStaticText(LangUtility.GetL(langCodes, "entries-keyword-wander"), titleFont, goWanderStringBounds)
					.AddSlider(SlidersWander, goWanderSliderBounds, "rangesWander")
					.AddSwitch(TogglesWander, goWanderSwitchBounds, "ordersWander")
					.AddStaticText(LangUtility.GetL(langCodes, "entries-keyword-follow"), titleFont, goFollowStringBounds)
					.AddSlider(SlidersFollow, goFollowSliderBounds, "rangesFollow")
					.AddSwitch(TogglesFollow, goFollowSwitchBounds, "ordersFollow")
					.AddStaticText(LangUtility.GetL(langCodes, "entries-keyword-engage"), titleFont, goEngageStringBounds)
					.AddSlider(SlidersEngage, goEngageSliderBounds, "rangesEngage")
					.AddSwitch(TogglesEngage, goEngageSwitchBounds, "ordersEngage")
					.AddStaticText(LangUtility.GetL(langCodes, "entries-keyword-pursue"), titleFont, goPursueStringBounds)
					.AddSlider(SlidersPursue, goPursueSliderBounds, "rangesPursue")
					.AddSwitch(TogglesPursue, goPursueSwitchBounds, "ordersPursue")
					.AddStaticText(LangUtility.GetL(langCodes, "entries-keyword-shifts"), titleFont, doShiftsStringBounds)
					.AddSlider(SlidersShifts, doShiftsSliderBounds, "timeofShifts")
					.AddSlider(SlidersFinish, doFinishSliderBounds, "finishShifts")
					.AddSwitch(TogglesShifts, doShiftsSwitchBounds, "ordersShifts")
					.AddStaticText(LangUtility.GetL(langCodes, "entries-keyword-patrol"), titleFont, goPatrolStringBounds)
					.AddTextInput(goPatrolInputsBounds, InputsPatrols, typedFont, "patrolPoints")
					.AddSwitch(TogglesPatrol, goPatrolSwitchBounds, "ordersPatrol")
					.AddButton(LangUtility.GetL(langCodes, "entries-keyword-return"), TogglesReturn, returnToButtonBounds, textsFont, EnumButtonStyle.Small, "ordersReturn")
				.EndIf()
			.EndChildElements()
			.Compose();

			SingleComposer.GetSwitch("ordersWander").SetValue(GetOrderValue("orderWander"));
			SingleComposer.GetSwitch("ordersFollow").SetValue(GetOrderValue("orderFollow"));
			SingleComposer.GetSwitch("ordersEngage").SetValue(GetOrderValue("orderEngage"));
			SingleComposer.GetSwitch("ordersPursue").SetValue(GetOrderValue("orderPursue"));
			SingleComposer.GetSwitch("ordersShifts").SetValue(GetOrderValue("orderShifts"));
			SingleComposer.GetSwitch("ordersPatrol").SetValue(GetOrderValue("orderPatrol"));
			
			SingleComposer.GetSlider("rangesWander").SetValues(Math.Clamp((int)GetFloatValue("wanderRange"), 0, 96), 0, (int)entity.WatchedAttributes.GetDouble("postRange"), 1, "m");
			SingleComposer.GetSlider("rangesFollow").SetValues(Math.Clamp((int)GetFloatValue("followRange"), 0, 96), 0, 32, 1, "m");
			SingleComposer.GetSlider("rangesEngage").SetValues(Math.Clamp((int)GetFloatValue("engageRange"), 0, 96), 0, 64, 1, "m");
			SingleComposer.GetSlider("rangesPursue").SetValues(Math.Clamp((int)GetFloatValue("pursueRange"), 0, 96), 0, 96, 1, "m");
			
			SingleComposer.GetSlider("timeofShifts").SetValues(Math.Clamp((int)GetFloatValue("shiftStarts"), 0, 96), 0, (int)capi.World.Calendar.HoursPerDay, 1, "h");
			SingleComposer.GetSlider("finishShifts").SetValues(Math.Clamp((int)GetFloatValue("shiftEnding"), 0, 96), 0, (int)capi.World.Calendar.HoursPerDay, 1, "h");
			SingleComposer.GetTextInput("patrolPoints").SetValue((string)GetVec3iValue("patrolVec3i"));
		}

		public override void OnFinalizeFrame(float dt) {
			base.OnFinalizeFrame(dt);
			entpos = entity.Pos.XYZ.Clone();
			entpos.Add(entity.SelectionBox.X2 - entity.OriginSelectionBox.X2, 0, entity.SelectionBox.Z2 - entity.OriginSelectionBox.Z2);
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
				if (pos.Z < 0) { return; }
				SingleComposer.Bounds.Alignment = EnumDialogArea.None;
				SingleComposer.Bounds.fixedOffsetX = 0;
				SingleComposer.Bounds.fixedOffsetY = 0;
				SingleComposer.Bounds.absFixedX = pos.X - SingleComposer.Bounds.OuterWidth / 2;
				SingleComposer.Bounds.absFixedY = capi.Render.FrameHeight - pos.Y - SingleComposer.Bounds.OuterHeight * Alignment;
				SingleComposer.Bounds.absMarginX = 0;
				SingleComposer.Bounds.absMarginY = 0;
			}
			base.OnRenderGUI(deltaTime);
		}

		protected bool OnGiveCommand(string orders, bool toggle) {
			SentryOrdersToServer newOrders = new SentryOrdersToServer();
			newOrders.playerUID = player.EntityId;
			newOrders.entityUID = entity.EntityId;
			switch (orders) {
				case "orderWander":
					newOrders.wandering = toggle;
					break;
				case "orderFollow":
					newOrders.following = toggle;
					newOrders.attribute = string.Join(',', "l", "guardedEntityId", (toggle ? player.EntityId : 0).ToString());
					newOrders.usedorder = true;
					break;
				case "orderEngage":
					newOrders.attacking = toggle;
					break;
				case "orderPursue":
					newOrders.pursueing = toggle;
					break;
				case "orderShifts":
					newOrders.shifttime = toggle;
					break;
				case "orderPatrol":
					newOrders.patroling = toggle;
					break;
				case "orderReturn":
					newOrders.returning = toggle;
					break;
			}
			capi.ShowChatMessage(LangUtility.GetL(langCodes, $"gui-{orders.Replace("order", "command-").ToLower()}-{toggle.ToString().ToLower()}"));
			capi.Network.GetChannel("sentrynetwork").SendPacket<SentryOrdersToServer>(newOrders);
			if (orders == "orderReturn") {
				return TryClose();
			}
			return true;
		}

		protected void OnCloseDialog() => TryClose();

		protected void SendInvPacket(object packet) => capi.Network.SendPacketClient(packet);

		protected void TogglesWander(bool toggle) => OnGiveCommand("orderWander", toggle); 

		protected void TogglesFollow(bool toggle) => OnGiveCommand("orderFollow", toggle);

		protected void TogglesEngage(bool toggle) => OnGiveCommand("orderEngage", toggle);

		protected void TogglesPursue(bool toggle) => OnGiveCommand("orderPursue", toggle);

		protected void TogglesShifts(bool toggle) => OnGiveCommand("orderShifts", toggle);
		
		protected void TogglesPatrol(bool toggle) => OnGiveCommand("orderPatrol", toggle);

		protected bool TogglesReturn() => OnGiveCommand("orderReturn", true);

		protected bool SlidersWander(int ranges) {
			entity.WatchedAttributes.SetFloat("wanderRange", ranges);
			return true;
		}

		protected bool SlidersFollow(int ranges) {
			entity.WatchedAttributes.SetFloat("followRange", ranges);
			return true;
		}

		protected bool SlidersPursue(int ranges) {
			entity.WatchedAttributes.SetFloat("pursueRange", ranges);
			return true;
		}

		protected bool SlidersEngage(int ranges) {
			entity.WatchedAttributes.SetFloat("engageRange", ranges);
			return true;
		}

		protected bool SlidersShifts(int shifts) {
			entity.WatchedAttributes.SetFloat("shiftStarts", shifts);
			return true;
		}

		protected bool SlidersFinish(int shifts) {
			entity.WatchedAttributes.SetFloat("shiftEnding", shifts);
			return true;
		}

		protected void InputsPatrols(string blocks) {
			try {
				// [3, 9, 20], [60, 20, 80], [20, 2, 9] //
				string[] allPoints = blocks.Replace(" ", "").Replace('(', '[').Replace(')', ']').Replace("],", "], ").Split(", ");
				List<Vec3i> coords = new List<Vec3i>();
				// [3,9,20], [60,20,80], [20,2,9] //
				foreach (string point in allPoints) {
					// [3,9,20] //
					string[] coord = point.Replace("[", "").Replace("]", "").Split(',');
					int numX = (int)Math.Ceiling(double.Parse(coord[0]) + capi.World.DefaultSpawnPosition.X);
					int numY = (int)Math.Ceiling(double.Parse(coord[1]));
					int numZ = (int)Math.Ceiling(double.Parse(coord[2]) + capi.World.DefaultSpawnPosition.Z);
					coords.Add(new Vec3i(numX, numY, numZ));
				}
				entity.WatchedAttributes.SetVec3is("patrolVec3i", coords.ToArray());
			} catch { }
		}

		protected bool GetOrderValue(string orders) {
			return entity.WatchedAttributes.GetBool(orders);
		}

		protected float GetFloatValue(string values) {
			if (!entity.WatchedAttributes.HasAttribute(values)) {
				entity.WatchedAttributes.SetFloat(values, 1f);
			}
			return entity.WatchedAttributes.TryGetFloat(values) ?? 0f;
		}

		protected string GetVec3iValue(string values) {
			if (!entity.WatchedAttributes.HasAttribute(values)) {
				entity.WatchedAttributes.SetVec3is(values, new Vec3i[] { entity.ServerPos.XYZ.AsVec3i });
			}
			Vec3i[] waypoints = entity.WatchedAttributes.GetVec3is(values);
			if (waypoints.Length == 0) {
				return "";
			}
			string[] strpoints = new string[waypoints.Length];
			for (int i = 0; i < waypoints.Length; i++) {
				int numX = (int)(waypoints[i].X - capi.World.DefaultSpawnPosition.X);
				int numY = (int)(waypoints[i].Y);
				int numZ = (int)(waypoints[i].Z - capi.World.DefaultSpawnPosition.Z);
				strpoints[i] = $"[{numX},{numY},{numZ}]";
			}
			return string.Join(", ", strpoints);
		}

		private bool InRange() => player.Pos.XYZ.Add(player.LocalEyePos).SquareDistanceTo(entity.Pos) < Math.Pow(player.Player.WorldData.PickingRange, 2);

		private string GetName() => entity.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("full") ?? Lang.GetL(langCodes, entity.Code.ToString());
	}
}