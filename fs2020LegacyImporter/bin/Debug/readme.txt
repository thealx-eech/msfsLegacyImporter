[SECURITY_NOTICE]

EXE file of this application is unsigned, so your Windows Defender or another antivirus software may be triggered. Sometimes it even block EXE file as malware (before or after archive extraction), it happen because of signatures comparison technology that does not analyze real program functionality but searching matches in binary data.
Each update build was submit into Microsoft Security Intelligence service to make sure it will be not blocked by Windows Defender, but it take days until cloud database, and then your client, will be updated. If you experience such issue - you may try to apply security intelligence manual update following this instruction [https://www.microsoft.com/en-us/wdsi/defenderupdates]  and then try to run application again.
If you are using some different antivirus software - you can send file for detailed analysis manually which available for most services. Usually it takes 2-4 hours to complete, as result EXE file will be whitelisted after next signatures update.
If it still does not work and you do not trust program developer - don't use it for now and wait for the release version.

[DESCRIPTION]

This is complex tool for importing Microsoft Flight Simulator X (FSX, 2006) aircraft into Microsoft Flight Simulator (MSFS, 2020). It does apply all required actions for successful import (like files copying, manifest file generation, critical issues fixing, 2D gauges conversion and other minor stuff). Two levels of import available - Basic and Full. First one providing list of fixes for various critical issues, lights and textures conversion, 2D gauges import. Second also have tools for CFG files manipulations and AIR file import.
Program still in development stage, but you can participate in testing and report about any issues you have. MSFS engine has its own limitations in Legacy aircraft support, be sure you are importing native FSX aircraft (not FS9 or P3D) before reporting issue.

[PROVIDED_FEATURES]

- copy files from FSX to MSFS Community folder with proper structure
- generate layout.json file with list of files
- generate manifest.json file based on loaded from aircraft package data (manual edit possible)
- load existing legacy or native MSFS aircraft
- split AIRCRAFT.CFG on relative CFG files
- insert missing values into imported CFG file(s)
- insert missing CFG file sections on user choice
- populate missing performance values from description
- fix ui_typerole invalid value
- insert external view gauges
- set speed indicator limits from cruise_speed value
- detect buggy engine values (engine_type and afterburner_available)
- adjust engines output power
- fix runway issues (gears raised, fuel pump disabled, parking brakes disabled)
- fix contact points duplicates (stuck landing gear issue)
- notify about contact point formatting issues
- convert exterior/interior lights
- insert taxi lights
- bulk convert of BMP textures to DDS
- import AIR values into CFG files
- import 2D gauges (WIP)
- toggle aircraft sounds
- insert/remove variometer tone
- backup and restore editable files
- inform about available program update, perform self update if triggered by user

[POSSIBLE_ISSUES]

- cockpit instruments do not work (not displayed or inoperate)
- aircraft does not appear in the list on main screen (especially gliders, try to use Select Aircraft window in Developer mode)
- aircraft appear in the list, but model is invisible because of unsupported model format
- engines switched off when player taking control of some aircraft on the runway (try Ctrl+E)
- turbine engines under powered at thrust level more than 40%
- inaccurate flight model
- sounds looping (like infinity raising gears sound)


[INSTALLATION]

- download the latest version archive from https://www.nexusmods.com/microsoftflightsimulator/mods/117
- unpack files into some folder, launch "msfsLegacyImporter.exe"

[UNINSTALL]

- delete folder

[REQUIREMENTS]

- Windows 7/8/10
- .NET 4.5

[VIDEO_TUTORIALS]

https://www.youtube.com/watch?v=Tt_6Vsp9xZY

https://www.youtube.com/watch?v=wNFbwr3KstE

https://www.youtube.com/watch?v=O80P73twn5E

https://www.youtube.com/watch?v=L77aG5UABS4

https://www.youtube.com/watch?v=g00a3mDRZIA

[NOTICE]

Since MSFS release, bug with legacy aircrafts exists - if you met some other player with same aircraft and exactly the same AC folder name (inside of SimObjects), game will crash for any of you. So it is not recommended to play imported airplanes online.

[CFG_PARSER]

Application loading all required CFG and FLT filed into the memory once aircraft loaded or imported, and updating local files values after each user action. 
If some files was edited manually while aircraft data loaded into the memory, notification appear that files should be reloaded.
To avoid parsing issues, keep files formatting properly (comment symbol is ";", no spaces inside of [SECTION_NAME], inactive parameters has ";-" at the beginning of line)

0. Main Screen
0.1 Left side - new import from FSX. On press, import form will appear. Original FSX aircraft folder should be selected first (usually inside of [FSX]\SimObjects\Airplanes\ but may be unpacked aircraft directory). Perfectly, it could be stock airplane with analogue 2D or 3D gauges. After Community folder is selected and fields populated, import can be performed. After success import tabs with available instruments will appear.
0.2 Right side - load MSFS aircraft. Button Browse will appear, after pressing it you'll need to find legacy or native aircraft folder inside of MSFS Community directory. After folder is selected, available CFG files and textures will be scanned, related tabs will show up. If you have not converted any aircraft yet (with Importer or any other method), use section 0.1 first. 

1. Init page

1.1 Current aircraft information
1.2 Update LAYOUT.JSON section can be triggered manually if you are moving/renaming files inside of aircraft. When you're using Importer features that affects files and folders (like CFG split of textures conversion), this option being triggered automatically, no need to press it after each operation.
1.3 Current import mode information. If current mode is Basic, Full import mode button will be at the bottom. Before proceed, important to check that all sections in AIRCRAFT.CFG labeled properly - in format like '[SECTION_NAME]' (not ';[SECTION_NAME]' or '[ SECTION NAME ]' ). When pressed, script will compare template CFGs (taken from MSFS generic plane) with aircraft.cfg and move presented sections in relative files. Original aircraft.cfg file will be renamed to ".aircraft.cfg" and ignored by JSON generator. You can remove it if you want but it does not affect the game anyway. After processing, layout.json will be regenerated automatically
1.4 If current mode is Full import, list of CFG files inside of aircraft folder. If all of them presented - no actions required. If some/all except aircraft.cfg are missing - Process button available.
1.5 If "unknown.cfg" file appear after processing - some of the sections was not found in templates, first thing to do is check that unrecognized sections labels written correctly. If you sure, that that section was not moved to proper CFG file by mistake - please report.

2. Aircraft

2.1 Missing or invalid aircraft parameters list. You can check all/some options and press the button below, script will try to search for selected values in aircraft description, but they may be not presented. If some of the notifies left unresolved, you can add missing values in aircraft.cfg file manually.
2.2 Sections list of this file. In green - presented sections (but it still may have some missing/disabled values), in orange - missing but not required sections, in red - highly likely missing required section for this aircraft.
2.3 Once some sections selected and Insert button pressed, you will be asked about new variables - if you press YES, all default values will be inserted into file as active parameters, if NO - inactive. It is recommended to choose YES for sections colored in RED.
2.4 If DLL files exists in aircraft folder, message appear about partially working avionics
2.5 Backup button available on all editable sections, once created - it can be clicked to restore original file. Backup file can be removed only manually.

3. Engines

3.1 Script checking values of critical parameters
3.2 If checkbox checked near some of the wrong parameter, they will be set to "0" after button pressed
3.3 Engines power rough adjustments buttons change value of "static_thrust" for turbine engine and power_scalar for piston engine
3.4 AIR data import available if TXT dump of AIR file exists in aircraft folder
3.5 To create AIR dump, get AirUpdate program ( http://www.mudpond.org/ ) and unpack it into Importer directory, launch it, select AIR file inside of aircraft folder, check "Full dump" and press "Dump" button. TXT file should be created in aircraft directory with same name as AIR file
3.6 All available values will appear in comparison table (AIR value / current value), check carefully that data match at least somehow
3.7 You can import all available values, or ignore zero/flat tables
3.8 After import, you'll need to fix all untouched modern values manually
3.9 List of sections at the bottom (same as 2.7-2.8)

4. Cockpit (Full import mode)

4.1 Script is checking for instruments and gauges presence (but not their statuses, so if some gauge marked in green it still can be disabled or not configured)
4.2 In red marked missing instruments, that appear on external view. So it is recommended to enable them.
4.3 After button pressed, template values for each selected gauge will be inserted into the cockpit.cfg file.
4.4 AIRSPEED indicator values will be updated automatically (if cruise_speed value set in AIRCRAFT.CFG/FLIGHT_MODEL.CFG) but that method is inaccurate. After adding instruments you have to apply manual corrections to their values manually in COCKPIT.CFG file.

5. Systems

5.1 List of lights listed here. If some of them in FSX format, you can select checkboxes and press convert button. FSX has much shorter list of light effects compare to MSFS so possibly you will see not exactly the same effect in the game. You may need to adjust direction coordinates after conversion (by default set to 0,0,0)
5.2 If aircraft does not have taxi or landing lights, you will see list of landing gear points, to which taxi lights can be attached (raised 2ft). No animation, so lights always stay in same position. You can always adjust their position manually (format: type, left/right, forward/backward, down/up).
5.3 List of sections (same as 2.7-2.8)

6. FlightModel

6.1 List of gears contact points that positioned too close (usually cause "stuck landing gear" bug).
6.2 When both points of same pair selected, they will be moved in their middle position.
6.3 If some points will be not properly formatted (like missing comma), they will be listed in red color. Manual fix required.
6.4 If no landing/taxi lights exists, you can attach taxi lights to contact points (+2ft higher +2ft forwards)
6.4 Missing critical flight model values list, some of them required only after AIR file import
6.4 AIR data import table same as in 3.4-3.8
6.5 To import all flight model values, you'll need to add AERODYNAMICS section. You may choose either option - insert default values or leave them disabled, but in second case you'll need to fix and enable them manually otherwise game will crash (all values inside of this section except "_table" are critical).
6.6 After both engines and flight model data imported, AIR file will be disabled and no longer readed by game. If some critical CFG values are not set, game may crash on loading process.
6.7 List of sections at the bottom (same as 2.7-2.8)

7. Runway (Full import mode only)

7.1 Configs section for Runway state (affect aircraft avionics state once you get controls on the ground).
7.2 May fix issues like raised landing gears, parking brakes disabled, fuel pumps/valves disabled

8. Textures

8.1 List of BMP textures inside of Textures (Textures.*) folder that should be converted
8.2 Two conversion tools available, you can try both if conversion failed with first one
8.3 On convert button press, BMPs converted into DX5 flipped DDS
8.4 Original BMPs will be stored as back up by adding dot to their names, but they can be removed anytime if DDS textures looks fine in the game

9. Models

9.1 Program read model.cfg file and get name of "interior" model, if ut exists. Clickable elements that model can be disabled to avoid game crash since MSFS ver1.8.3.0
9.2 After Remove button pressed, backup of MDL file will be created (only if it does not exists) and _CVC_ folder (cache) of this aircraft cleared
9.3 Original MDL can be restored by clicking button in right top corner of Models tab

10 Sounds

10.1 List of sound sample, used by aircraft. Each sound can be enabled and disabled anytime.
10.2 If sound.xml file does not exist yet, variometer tone button available and it volume adjustment.
10.3 If sound.xml exists, removal button will be there.

11. Panel

11.1 Experimental feature for 2D gauges conversion
11.2 If you have FSX installed, you need to extract default instruments sources first by using top button, they will be stored in "\Community\legacy-vcockpits-instruments\.FSX" directory. Without default instruments sources some/all gauges aircraft may not work. However, DLL sources are not supported yet, so some gauges just can't be converted.
11.3 If aircraft has CAB file included, you can use another button to extract these sources. If instruments already unpacked in panel folder, no actions required. You can edit these sources (both images and XML files) as they will be not changed by Importer anymore.
11.4 To available convert panels, check some of them and press Import button.
11.5 To adjust moving elements and individual gauges backgrounds brightness, use slider. Lower value makes image brighter, higher - darker.
11.6 If you see needles in the cockpit, but no gauges backgrounds appear - check "Force gauge background" option
11.7 If you experience any problems with imported gauges, you can try again with next update - each fixed issue may affect any FSX aircraft.
11.8 Possible results:
11.8.1 Gauges may get same view as originally in FSX and work as expected, i.e. conversion succeed
11.8.2 Gauges may get texture background and wireframe removed from it, even if it will not functioning properly; wait for updates or check generated JS files
11.8.3 Game crashes or no gauges appear (try to check "Force gauge background") or you see total mess in the cockpit (you can try to copy required files that mentioned in warning messages)
11.8.4 App crashes when you press one of the import buttons, usually because of XML file format issues (feel free to report)
11.9 To remove generated panels: Press "Restore Backup" button on Panel tab, delete /Community/legacy-vcockpits-instruments folder

12. About

12.1 Update availability. If update available, tab label will be colored in red
12.2 Manual update will open zip link in the browser
12.3 Self update will pull update archive from the server, unpack it, replace EXEs and restart process

[CREDITS]

Inspired by Klas Björkqvist's "planeconverter"
Developed with assistance of MFS SDK Discord channel and MS Flight Simulator Gliders FB group members
For AIR data import conversion table was used made by OzWookiee
Thanks everyone for bug reports! It really save my time on testing.
