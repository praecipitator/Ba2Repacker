using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Synthesis.Settings;

namespace Ba2Repacker
{

    internal class Settings
    {
        [SynthesisOrder]
        [SynthesisSettingName("Main BA2 Limit")]
        [SynthesisTooltip("Main BA2 repacking will happen if you have more main BA2s, master files, and cdx/csg files than this.")]
        public int Ba2Limit = 400;

        [SynthesisOrder]
        [SynthesisSettingName("Texture BA2 Limit")]
        [SynthesisTooltip("Texture BA2 repacking will happen if have more texture BA2s than this.")]
        public int TextureLimit = 400;

        [SynthesisOrder]
        [SynthesisSettingName("Maximal Filesize (MB)")]
        [SynthesisTooltip("Never repack BA2s larger than this")]
        public int MaxFileSize = 100;

        [SynthesisOrder]
        [SynthesisSettingName("Repack CreationClub Mods")]
        [SynthesisTooltip("If set to Manual, CC mods are treated just like regular mods, and can be white- or blacklisted. Always and Never will either include or exclude them all at once, overriding the lists.")]
        [SynthesisStaticEnumDictionary]
        public InclusionMode ccModsSetting = InclusionMode.Manual;

        [SynthesisOrder]
        [SynthesisSettingName("Whitelist Mode")]
        [SynthesisTooltip("If enabled, only mods in the whitelist are eligible for repacking. Otherwise, all mods except blacklisted and the game's base files are eligible.")]
        public bool whitelistMode = true;

        [SynthesisOrder]
        [SynthesisSettingName("Mod Blacklist")]
        [SynthesisTooltip("If Whitelist Mode is disabled, these mods will be never eligible for repacking")]
        public List<ModKey> modBlacklist = new();

        [SynthesisOrder]
        [SynthesisSettingName("Mod Whitelist")]
        [SynthesisTooltip("If Whitelist Mode is enabled, only these mods will be eligible for repacking")]
        public List<ModKey> modWhitelist = new();

        [SynthesisOrder]
        [SynthesisSettingName("Disabled BA2 suffix")]
        [SynthesisTooltip("This will be appended to the filename of a BA2 which has been repacked")]
        public string disabledSuffix = ".repacked";


        [SynthesisOrder]
        [SynthesisSettingName("MO2 Settings")]
        public Section_MO2Settings mo2Settings = new();

        [SynthesisOrder]
        [SynthesisSettingName("Debug Settings")]
        public Section_DebugSettings debugSettings = new();
    }

    internal class Section_MO2Settings
    {
        [SynthesisOrder]
        [SynthesisSettingName("Use MO2 mode, if possible")]
        [SynthesisTooltip("If enabled, the patcher will try to detect whenever it is being run through MO2, and switch to MO2 mode if necessary.")]
        public bool useAutoMO2mode = true;

        [SynthesisOrder]
        [SynthesisSettingName("Override MO2 Profile Name")]
        [SynthesisTooltip("If filled out, the patcher will attempt to use this profile, instead of selected_profile from the INI")]
        public string profileOverride = "";

        [SynthesisOrder]
        [SynthesisSettingName("Override ModOrganizer.ini path")]
        [SynthesisTooltip("A full path to a ModOrganizer.ini file. If filled out, this ini file will be used instead of the default auto-discover logic.")]
        public string iniOverride = "";
    }


    internal class Section_DebugSettings
    {
        [SynthesisOrder]
        [SynthesisSettingName("Increase Verbosity")]
        [SynthesisTooltip("If enabled, more output will be generated")]
        public bool verboseMode = true;
    }

    internal enum InclusionMode
    {
        Always,
        Never,
        Manual
    }
}
