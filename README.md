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
BA2s above this size will never be repacked.

### Repack CreationClub Mods:
This has three options: "Always", "Never", "Manual" (default).
"Manual" means, CC mods aren't being treated different from any other mods, and will follow black- or whitelist rules.
"Always" and "Never" set CC mods to be either always or never repackable respectively, overriding any black- or whitelisting.
Keep in mind that CC mods can have their BA2s restored, when you revalidate files via Steam. You would need to run the Synthesis pipeline again after that.


### Whitelist Mode
In whitelist mode, only mods specified in the whitelist can have their BA2s repacked, and nothing else.
Otherwise, all mods except the ones specified in the blacklist below are.
Keep in mind that vanilla files are NEVER repacked, no matter the lists, and the settings "Always" or "Never" of the option "Repack CreationClub Mods"
will override any black- or whitelisting.

### Disabled BA2 suffix
When a BA2 was used for repacking, this will be added to it's filename. Keep in mind that the patcher also uses this suffix to find repacked BA2s when restoring them,
so only change it if you are know what you're doing!
Default: ".repacked"

### Undo Mode
If enabled, next time the patcher runs, it will restore all repacked BA2s to their original states, undoing all repacking. 
Disable Undo Mode in order to repack again.

### Repack Localized Files
Repacking localized files will interfere with other patchers, especially item taggers.
This is safe to enable under two circumstances:
* You do not use any other patchers except the Ba2 Repacker
* You add another copy of the Ba2 Repacker to the begin of the pipeline, and enable "Undo Mode" in that copy. Also make sure that this "Undoer" has "Update MO2 VFS" enabled, or the patchers after it won't see the restored BA2s.


### MO2 Settings:

#### Use MO2 mode, if possible:
If enabled, the patcher will try to detect whenever it is being run through MO2, and switch to MO2 mode if necessary.
MO2 mode means, the patcher will attempt to make sure that repacked BA2s stay within the proper MO2 mod subfolder,
otherwise, they will all end up in Overwrite.
This should now work with both regular and portable installations.

#### Update MO2 VFS:
If enabled and MO2 has been detected, the repacker will try to launch a dummy BAT file through MO2, forcing it to refresh it's Virtual File System.
This makes sure that whatever files the repacker changed, any patchers running after it will see these changes. 
This is especially relevant if you run a repacker in "Undo Mode": without this setting, any following patchers won't see the restored original BA2s, rendering the "Undoer" moot.

#### Override MO2 Profile Name:
If left empty, the patcher will try to read the current selected MO2 profile from the config files.
Otherwise, the currently-used profile will be used ("selected_profile" in the current ModOrganizer.ini).


#### Override ModOrganizer.ini path:
If the automatic MO2 detection isn't working for you, you can specify a full path to your ModOrganizer.ini file here.



## Disclaimer: 
This patcher seems to have worked fine so far, but, due to the way it works, bugs might affect the archives of your installed mods.
Use at your own risk.
