This is a [Mutagen](https://github.com/Mutagen-Modding/Mutagen)-based autopatcher for Fallout 4, intended for use with [Synthesis](https://github.com/Mutagen-Modding/Synthesis).

# Description
Combines several smaller BA2s into one large one, as a workaround for the BA2 limit.
BA2s get chosen for repacking by file size (smallest first). 
Files which were used for repacking are renamed to ".repacked" (configurable). 
This gets reversed before the next run.

Vanilla files are NEVER repacked. For CreationClub mods, this can be enabled via the settings.

## Settings

### Main BA2 Limit:
The patcher will add up all currently installed non-texture BA2s, master files, ESLs, as well as CDX and CSG files.
If that number is higher than the setting "Main BA2 Limit", main BA2s will be precombined to get below it again.

### Texture BA2 Limit:
Texture BA2s seem to have a different limit, and causes visual issues in-game, instead of a crash after the intro.
For this limit, only the Texture BA2s are counted and must be below the configured number.

### Maximal Filesize (MB):
BA2s above this size will never be repacked

### Exclude CreationClub Mods:
CC mods can have their BA2s restored, when you revalidate files via Steam. For this reason, they are excluded by default.
Disabling this option will allow the patcher to repack them as well.

### Whitelist Mode
In whitelist mode, only mods specified below can have their BA2s repacked, and nothing else.
Otherwise, all mods except the ones specified in the blacklist below are.
Keep in mind that vanilla files are NEVER repacked, no matter the lists, and the setting "Exclude CreationClub Mods" trumps whitelisting.

### Disabled BA2 suffix
When a BA2 was used for repacking, this will be added to it's filename.

### MO2 Settings:
#### Use MO2 mode, if possible:
If enabled, the patcher will try to detect whenever it is being run through MO2, and switch to MO2 mode if necessary.
MO2 mode means, the patcher will attempt to make sure that repacked BA2s stay within the proper MO2 mod subfolder,
otherwise, they will all end up in Overwrite.
This should now work with both regular and portable installations.

#### Override MO2 Profile Name:
If left empty, the patcher will try to read the current selected MO2 profile from the config files.
Otherwise, this profile will be used instead.


## Disclaimer: 
This patcher seems to have worked fine so far, but, due to the way it works, bugs might affect the archives of your installed mods.
Use at your own risk.
