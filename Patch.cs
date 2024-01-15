// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.Overworld;

namespace EchKode.PBMods.CrashedUnitPilotView
{
	[HarmonyPatch]
	public static class Patch
	{
		[HarmonyPatch(typeof(CIViewCombatUnitData), nameof(CIViewCombatUnitData.OnPilotStateRedraw))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civcud_OnPilotStateRedrawTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// If the unit is crashing, also check if the combat overlay modifier is being pressed and show
			// the normal pilot view if it is. Otherwise, show the unit crashing warning.

			var cm = new CodeMatcher(instructions, generator);
			var isCrashingMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(CombatEntity), nameof(CombatEntity.isCrashing));
			var isFeatureUnlockedMethodInfo = AccessTools.DeclaredMethod(
				typeof(OverworldUtility),
				nameof(OverworldUtility.IsFeatureUnlocked),
				new System.Type[]
				{
					typeof(string),
				});
			var getButtonMethodInfo = AccessTools.DeclaredMethod(
				typeof(Rewired.Player),
				nameof(Rewired.Player.GetButton),
				new System.Type[]
				{
					typeof(int),
				});
			var isCrashingMatch = new CodeMatch(OpCodes.Callvirt, isCrashingMethodInfo);
			var isFeatureUnlockedMatch = new CodeMatch(OpCodes.Call, isFeatureUnlockedMethodInfo);
			var compareEqualMatch = new CodeMatch(OpCodes.Ceq);
			var loadPlayer = CodeInstruction.LoadField(typeof(InputHelper), nameof(InputHelper.player));
			var loadInputAction = CodeInstruction.LoadField(typeof(InputAction), nameof(InputAction.CombatToggleOverlay));
			var getButton = new CodeInstruction(OpCodes.Callvirt, getButtonMethodInfo);

			cm.MatchStartForward(isCrashingMatch)
				.Advance(2);
			cm.CreateLabel(out var noCrashLabel);
			var branchNoCrash = new CodeInstruction(OpCodes.Brfalse_S, noCrashLabel);

			cm.Advance(-1)
				.InsertAndAdvance(branchNoCrash)
				.InsertAndAdvance(loadPlayer)
				.InsertAndAdvance(loadInputAction)
				.InsertAndAdvance(getButton)
				.SetOpcodeAndAdvance(OpCodes.Brfalse_S);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(CIHelperOverlays), nameof(CIHelperOverlays.OnModifierUpdate))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Ciho_OnModifierUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// This is the method that shows/hides the stat overlays on the units in combat.
			// It has a nifty guard at the beginning to keep from doing too much work when the
			// modifier state hasn't changed from the frame before. Hook in a call to refresh
			// the pilot view at the end and take advantage of that guard.

			var cm = new CodeMatcher(instructions, generator);
			var callAnyPilotUpdate = CodeInstruction.Call(typeof(CIControllerCombat), nameof(CIControllerCombat.OnAnyPilotUpdate));

			cm.End();
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();

			cm.Insert(callAnyPilotUpdate)
				.AddLabels(labels);

			return cm.InstructionEnumeration();
		}
	}
}
