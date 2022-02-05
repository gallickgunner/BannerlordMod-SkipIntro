using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using SandBox;

namespace SkipIntro
{
	[HarmonyPatch]
	static class SkipIntroCore
	{
		public static Dictionary<string, Vec2> _startingPoints = new Dictionary<string, Vec2>
		{
			{
				"empire",
				new Vec2(657.95f, 279.08f)
			},
			{
				"sturgia",
				new Vec2(356.75f, 551.52f)
			},
			{
				"aserai",
				new Vec2(300.78f, 259.99f)
			},
			{
				"battania",
				new Vec2(293.64f, 446.39f)
			},
			{
				"khuzait",
				new Vec2(680.73f, 480.8f)
			},
			{
				"vlandia",
				new Vec2(207.04f, 389.04f)
			}
		};
		
		[HarmonyTranspiler]
		[HarmonyPatch(typeof(TaleWorlds.MountAndBlade.Module), "SetInitialModuleScreenAsRootScreen")]
		public static IEnumerable<CodeInstruction> SetInitialModuleScreenAsRootScreen(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			MethodInfo referenceMethod = AccessTools.Method(typeof(TaleWorlds.MountAndBlade.Module), "OnInitialModuleScreenActivated");
			MethodInfo UtilitiesDisableLW = SymbolExtensions.GetMethodInfo(() => Utilities.DisableGlobalLoadingWindow());
			MethodInfo LWDisableLW = SymbolExtensions.GetMethodInfo(() => LoadingWindow.DisableGlobalLoadingWindow());
			FieldInfo toMatchFI = AccessTools.Field(typeof(TaleWorlds.MountAndBlade.Module), "_splashScreenPlayed");

			CodeInstruction prevInstruc = new CodeInstruction(OpCodes.Nop);
			Label funcCall = generator.DefineLabel();
			foreach (var instruc in instructions)
			{
				if (prevInstruc.opcode == OpCodes.Ldfld && prevInstruc.operand as FieldInfo == toMatchFI)
				{
					yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ConfigFileManager), "SkipMainIntro"));
					yield return new CodeInstruction(OpCodes.Or);
				}
				else if (instruc.Calls(referenceMethod))
				{
					yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ConfigFileManager), "SkipMainIntro"));
					yield return new CodeInstruction(OpCodes.Brfalse_S, funcCall);
					yield return new CodeInstruction(OpCodes.Call, LWDisableLW);
					yield return new CodeInstruction(OpCodes.Call, UtilitiesDisableLW);
					instruc.labels.Add(funcCall);
				}
				prevInstruc = instruc;
				yield return instruc;
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(SandBoxGameManager), "OnLoadFinished")]
		public static IEnumerable<CodeInstruction> onLoadFinished(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo fromMethod = AccessTools.Method(typeof(SandBoxGameManager), "LaunchSandboxCharacterCreation");
			MethodInfo toMethod = SymbolExtensions.GetMethodInfo(() => SkipIntroCore.HandleQuickStart());
			MethodInfo GetDevMode = AccessTools.PropertyGetter(typeof(TaleWorlds.Core.Game), "IsDevelopmentMode");
			CodeInstruction prevInstruc = new CodeInstruction(OpCodes.Nop);			
			Label? funcEnd;
			foreach (var instruc in instructions)
			{
				if (instruc.operand as MethodInfo == fromMethod)
                {
					if (instruc.opcode == OpCodes.Ldftn)
					{
						yield return new CodeInstruction(OpCodes.Pop);
						yield return new CodeInstruction(OpCodes.Ldnull);
						instruc.operand = toMethod;
					}
				}
				
				if (prevInstruc.Calls(GetDevMode) && instruc.Branches(out funcEnd))
				{
					yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ConfigFileManager), "SkipSandboxIntro"));
					yield return new CodeInstruction(OpCodes.Or);
				}
				prevInstruc = instruc;
				yield return instruc;
			}
		}

		[HarmonyReversePatch]
		[HarmonyPatch(typeof(SandBoxGameManager), "LaunchSandboxCharacterCreation")]
		public static void LaunchSandboxCharacterCreation()
		{
			//fake code to prevent inlining.
			throw new NotImplementedException("It's a stub");
		}

		public static IEnumerable<CultureObject> GetCultures()
		{
			foreach (CultureObject cultureObject in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
			{
				if (cultureObject.IsMainCulture)
				{
					yield return cultureObject;
				}
			}
		}

		public static void HandleQuickStart()
		{
			if(!ConfigFileManager.QuickStart)
            {
				LaunchSandboxCharacterCreation();
				return;
			}

			/* Skip creating CharacterCreationStages and what not entirely. Instead set values of Hero and Clan directly
			 * and initialize the call to MapState to load campaign.
			 */

			/*
			 * 1)  <CharacterCreationState> calls initializes which traces back to <CharacterCreationContentBase> which calls 
			 * 
			 * this.initializeMainheroStats()
			 */

			// Initialize Hero SKills/Attributes
			Hero.MainHero.HeroDeveloper.ClearHero();
			Hero.MainHero.HitPoints = 100;
			Hero.MainHero.SetBirthDay(CampaignTime.YearsFromNow(-20));
			Hero.MainHero.SetName(new TextObject("Umer"), null);
			Hero.MainHero.HeroDeveloper.UnspentFocusPoints = 15;
			Hero.MainHero.HeroDeveloper.UnspentAttributePoints = 15;
			Hero.MainHero.HeroDeveloper.SetInitialLevel(1);

			foreach (SkillObject skill in Skills.All)
			{
				Hero.MainHero.HeroDeveloper.InitializeSkillXp(skill);
			}
			foreach (CharacterAttribute attrib in Attributes.All)
			{
				Hero.MainHero.HeroDeveloper.AddAttribute(attrib, 2, false);
			}
			
			//Apply Culture
			Clan.PlayerClan.ChangeClanName(Helpers.FactionHelper.GenerateClanNameforPlayer());
			CharacterObject.PlayerCharacter.Culture = SkipIntroCore.GetCultures().GetRandomElementInefficiently<CultureObject>();
			Clan.PlayerClan.Culture = CharacterObject.PlayerCharacter.Culture;
			Clan.PlayerClan.UpdateHomeSettlement(null);
			Clan.PlayerClan.Renown = 0f;
			Hero.MainHero.BornSettlement = Clan.PlayerClan.HomeSettlement;

			//Can apply equipments here but we skip, since goal is to test campaign. We can make do with random/default.


			/* 2) <CharacterCreationState> calls <FinalizeCharacterCreation()>
			 * This calls 
			 * 
			 * CharacterCreationScreen.OnCharacterCreationFinalized()
			 * this.CurrentCharacterCreationContent.OnCharacterCreationFinalized();
			 * CampaignEventDispatcher.Instance.OnCharacterCreationIsOver();
			 * 
			 * These calls mainly do the main work of applying culture and initializing main hero stats which we have done above,
			 * Hence we only need to initialize MapState now and teleport the camera onto map where party is.
			 */
			LoadingWindow.EnableGlobalLoadingWindow();
			Game.Current.GameStateManager.CleanAndPushState(Game.Current.GameStateManager.CreateState<MapState>(), 0);
			PartyBase.MainParty.Visuals.SetMapIconAsDirty();
			CultureObject culture = CharacterObject.PlayerCharacter.Culture;

			Vec2 position2D;
			if (_startingPoints.TryGetValue(culture.StringId, out position2D))
			{
				MobileParty.MainParty.Position2D = position2D;
			}
			else
			{
				MobileParty.MainParty.Position2D = Campaign.Current.DefaultStartingPosition;
				FileLog.Log("Selected culture is not in the dictionary!" + "\r\nIn HandleQuickStart(), Line No: 224 at <SkipIntro.cs>");
			}
			CampaignEventDispatcher.Instance.OnCharacterCreationIsOver();

			MapState mapState;
			if ((mapState = (GameStateManager.Current.ActiveState as MapState)) != null)
			{
				mapState.Handler.ResetCamera();
				mapState.Handler.TeleportCameraToMainParty();
			}
		}
	}
}
