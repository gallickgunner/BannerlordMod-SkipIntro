using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SkipIntro
{
    public class SkipIntroSubModule : MBSubModuleBase
	{
		private static string error = "";
		protected override void OnSubModuleLoad()
		{
			base.OnSubModuleLoad();
			ConfigFileManager.loadConfigFile(out error);
			Harmony harmony = new Harmony("SkipIntro");
			harmony.PatchAll();
		}
		protected override void OnBeforeInitialModuleScreenSetAsRoot()
		{
			if (!string.IsNullOrEmpty(error))
				InformationManager.DisplayMessage(new InformationMessage(error));
			base.OnBeforeInitialModuleScreenSetAsRoot();
		}
	}
}