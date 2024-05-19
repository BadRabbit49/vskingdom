using System.Text;
using System;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.Essentials;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class EntityBehaviorAITasking : EntityBehaviorTaskAI {
		public EntityBehaviorAITasking(Entity entity) : base(entity) {
			TaskManager = new AiTaskManager(entity);
		}

		private IAiTask MeleeAttack { get; set; }
		private IAiTask RangeAttack { get; set; }

		public override string PropertyName() {
			return "SoldierAITasking";
		}

		public override void Initialize(EntityProperties properties, JsonObject aiconfig) {
			if (!(entity is EntityAgent)) {
				entity.World.Logger.Error("The task ai currently only works on entities inheriting from EntityAgent. Will ignore loading tasks for entity {0} ", entity.Code);
				return;
			}
			PathTraverser = new WaypointsTraverser(entity as EntityAgent);
			JsonObject[] array = aiconfig["aitasks"]?.AsArray();
			if (array == null) {
				return;
			}
			JsonObject[] array2 = array;
			foreach (JsonObject jsonObject in array2) {
				string text = jsonObject["code"]?.AsString();
				if (!jsonObject["enabled"].AsBool(defaultValue: true)) {
					continue;
				}
				Type value = null;
				if (!AiTaskRegistry.TaskTypes.TryGetValue(text, out value)) {
					entity.World.Logger.Error("Task with code {0} for entity {1} does not exist. Ignoring.", text, entity.Code);
					continue;
				}
				IAiTask aiTask = (IAiTask)Activator.CreateInstance(value, (EntityAgent)entity);
				try {
					aiTask.LoadConfig(jsonObject, aiconfig);
				} catch (Exception) {
					entity.World.Logger.Error("Task with code {0} for entity {1}: Unable to load json code.", text, entity.Code);
					throw;
				}
				if (text == "SoldierMeleeAttack") {
					MeleeAttack = aiTask;
					continue;
				}
				if (text == "SoldierRangeAttack") {
					RangeAttack = aiTask;
					continue;
				}
				TaskManager.AddTask(aiTask);
			}
		}

		public override void GetInfoText(StringBuilder infotext) { }

		public override void OnEntityLoaded() {
			base.OnEntityLoaded();
			if (RegisteredItems.AcceptedRange.Contains((entity as EntityArcher)?.RightHandItemSlot?.Itemstack?.Collectible?.Code)) {
				SwapAttacksTo(Specialization.RANGE);
			} else {
				SwapAttacksTo(Specialization.MELEE);
			}
		}

		public virtual void SwapAttacksTo(Specialization specialization) {
			entity.World.Logger.Notification("SWAPPING TO " + specialization.ToString());
			foreach (var task in TaskManager.AllTasks) {
				if (task.Id == RangeAttack.Id || task.Id == MeleeAttack.Id) {
					TaskManager.StopTask(task.GetType());
					TaskManager.RemoveTask(task);
					continue;
				}
				entity.World.Logger.Notification(task.Priority + " | " + task.ToString() + " | " + task.Id);
			}
			if (specialization == Specialization.MELEE) {
				TaskManager.AddTask(MeleeAttack);
			}
			if (specialization == Specialization.RANGE) {
				TaskManager.AddTask(RangeAttack);
			}
			
		}
	}
}
