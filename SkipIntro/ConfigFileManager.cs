using System;
using System.IO;

namespace SkipIntro
{
	internal static class ConfigFileManager
	{
		private static string assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
		private static string parentDir = Directory.GetParent(Directory.GetParent(assemblyDir).ToString()).ToString();

		private static string cfgPath = parentDir + "\\SkipIntro.cfg";
		private static bool _skipMainIntro = true;
		private static bool _skipSandboxIntro = true;
		private static bool _quickStart = false;

		public static bool SkipMainIntro
        {
			get { return _skipMainIntro; }
        }

		public static bool SkipSandboxIntro
		{
			get { return _skipSandboxIntro; }
		}
		public static bool QuickStart
		{
			get { return _quickStart; }
		}

		public static bool loadConfigFile(out string error)
		{
			bool err = false;
			error = "";
			if (File.Exists(cfgPath))
			{				
				foreach (string line in File.ReadLines(cfgPath))
				{
					if(!line.StartsWith("#") && !string.IsNullOrEmpty(line))
					{
						string[] option = line.Split(new char[] { '=' });
						
						if (option[0] == "skipMainIntro")
							err = !Boolean.TryParse(option[1], out _skipMainIntro);
						else if (option[0] == "skipCampaignIntro")
							err = !Boolean.TryParse(option[1], out _skipSandboxIntro);
						else if (option[0] == "skipCC")
							err = !Boolean.TryParse(option[1], out _quickStart);

						if (err)
						{
							error = "Error parsing options. Make sure there are no whitespaces and" +
								" use 1 or 0 as values inside config file. Videos will be skipped by default.";
							_skipMainIntro = true;
							_skipSandboxIntro = true;
							return false;
						}
					}
				}					
				return !err;
			}
			else
			{
				error = "Error finding the config file. Videos will be skipped by default.";
				return err;
			}
		}
	}
}
