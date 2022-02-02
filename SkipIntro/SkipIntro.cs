using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using SandBox;

namespace SkipIntro
{
	[HarmonyPatch]
	internal class SkipIntro
	{		
		private static Dictionary<string, Vec2> _startingPoints = new Dictionary<string, Vec2>
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

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Module), "SetInitialModuleScreenAsRootScreen")]
		public static void SetInitialModuleScreenAsRootScreen(ref bool ____splashScreenPlayed)
		{
			if(ConfigFileManager.SkipMainIntro)
				____splashScreenPlayed = true;
		}
		[HarmonyReversePatch]
		[HarmonyPatch(typeof(SandBoxGameManager), "LaunchSandboxCharacterCreation")]
		private static void LaunchSandboxCharacterCreation()
		{
			//fake code to prevent inlining.
			throw new NotImplementedException("It's a stub");
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(SandBoxGameManager), "OnLoadFinished")]
		public static bool OnLoadFinished(ref SandBoxGameManager __instance, bool ____loadingSavedGame)
		{
			if (!____loadingSavedGame)
			{
				MBDebug.Print("Switching to menu window...", 0, Debug.DebugColor.White, 17592186044416UL);
				if (!Game.Current.IsDevelopmentMode && !ConfigFileManager.SkipSandboxIntro)
				{
					VideoPlaybackState videoPlaybackState = Game.Current.GameStateManager.CreateState<VideoPlaybackState>();
					string moduleFullPath = ModuleHelper.GetModuleFullPath("SandBox");
					string videoPath = moduleFullPath + "Videos/campaign_intro.ivf";
					string audioPath = moduleFullPath + "Videos/campaign_intro.ogg";
					string subtitleFileBasePath = moduleFullPath + "Videos/campaign_intro";
					videoPlaybackState.SetStartingParameters(videoPath, audioPath, subtitleFileBasePath, 60f, true);

					// skip character creation if option set in config file
					videoPlaybackState.SetOnVideoFinisedDelegate(new Action(ConfigFileManager.QuickStart ? (Action)HandleQuickStart : LaunchSandboxCharacterCreation));
					Game.Current.GameStateManager.CleanAndPushState(videoPlaybackState, 0);
				}
				else
				{
					if (ConfigFileManager.QuickStart)
						HandleQuickStart();
					else
						LaunchSandboxCharacterCreation();
				}
				System.Reflection.PropertyInfo pi = AccessTools.Property(typeof(SandBoxGameManager).BaseType, "IsLoaded");
				pi.SetValue(__instance, true);
				return false;
			}
			return true;			
		}

		private static IEnumerable<CultureObject> GetCultures()
		{
			foreach (CultureObject cultureObject in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
			{
				if (cultureObject.IsMainCulture)
				{
					yield return cultureObject;
				}
			}
			List<CultureObject>.Enumerator enumerator = default(List<CultureObject>.Enumerator);
			yield break;
			yield break;
		}

		private static void HandleQuickStart()
		{
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
			int sk = Hero.MainHero.GetSkillValue(DefaultSkills.Scouting);
			int xp = Campaign.Current.Models.CharacterDevelopmentModel.GetXpRequiredForSkillLevel(sk);
			HeroDeveloper hd = Hero.MainHero.HeroDeveloper;
			Hero hero = Hero.MainHero;
			
			//Apply Culture
			Clan.PlayerClan.ChangeClanName(Helpers.FactionHelper.GenerateClanNameforPlayer());
			CharacterObject.PlayerCharacter.Culture = SkipIntro.GetCultures().GetRandomElementInefficiently<CultureObject>();
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
				Debug.FailedAssert("Selected culture is not in the dictionary!", "C:\\Develop\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CharacterCreationContent\\SandboxCharacterCreationContent.cs", "OnCharacterCreationFinalized", 104);
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
