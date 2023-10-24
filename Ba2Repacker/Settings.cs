using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Synthesis.Settings;

namespace Ba2Repacker
{
    internal class Section_MO2Settings
    {
        [SynthesisOrder]
        [SynthesisSettingName("Enable MO2 Mode")]
        [SynthesisTooltip("The patcher will attempt to find your current MO2 profile, and keep the disabled BA2 files within the corresponding mod directories")]
        public bool EnableMO2Mode = false;

        [SynthesisOrder]
        [SynthesisSettingName("Override MO2 Profile Name")]
        [SynthesisTooltip("If filled out, the patcher will attempt to use this profile, instead of ")]
        public string profileOverride = "";
    }

    internal class Settings
    {
        [SynthesisOrder]
        [SynthesisSettingName("MO2 Settings")]
        public Section_MO2Settings mo2Settings = new();

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
        [SynthesisSettingName("Exclude CreationClub Mods")]
        [SynthesisTooltip("Exclude Bethesda's CC mods, even if they would be included otherwise. Because a Steam file revalidation will bring these BA2s back.")]
        public bool skipCCmods = true;

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
    }
}
