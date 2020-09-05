[DESCRIPTION]

This program was made to simplify FSX aircraft import into MSFS. At this moment, it can be named as assistant, as just perform routine import actions, and also provide some guidance on how to fix detected issues. Maybe some day it will became converter, but now you should make a lot of manual changes in aircraft files to make it work completely properly.

[PROVIDED_FEATURES]

- copy files from FSX to MSFS Community folder with proper structure
- generate layout.json file with list of files
- generate manifest.json file based on typed by user data
- load existing legacy MSFS aircraft
- split aircraft.cfg on relative cfg files
- insert missing gauges parameters from templates (does not fixing missing cockpit gauges!), inserted values should be adjusted manually
- detect buggy engine values (engine_type and afterburner_available)
- convert external and cockpit lights (poorly tested)
- convert BMP textures (poorly tested)
- inform about available program update, perform self update if required

[NOTICE.1]

This is an early test version, so use it only if you have some experience and knowledge in this process. This program manipulate with files and reading/writing registry values, so use it on your own risk. Application is not signed, so possibly you antivirus will ask permission to launch it.

[NOTICE.2]

This converter for personal use only. Do not publish converted planes. If you are owner of the model, you can contact me about issues you have and maybe I can fix some.

[NOTICE.3]

In release MSFS version bug with legacy aircrafts exists - if you met some other player with same aircraft and exactly the same AC folder name (inside of SimObjects), game will crash for any of you. So it is not recommended to play imported airplanes online.

[INSTALLATION]

- dowbload latest archive from http://eech.online/msfslegacyimporter/
- unpack files into same folder, launch EXE

[UNINSTALL]

Delete folder

[REQUIREMENTS]

- Windows 7/8/10
- .NET 4.5

[DETAILED_DESCRIPTION]

Init page

- 1st section is Imported aircraft. After Browse button pressed, you'll need to find legacy aircraft folder inside of MSFS Community. After folder is selected, available CFG files and textures will be scanned, related tabs will show up.
- Scan section can be triggered manually if you moving/renaming files inside of legacy aircraft.
- Import FSX aircraft section - original FSX aircraft folder should be selected (usually inside of [FSX]\SimObjects\Airplanes\). Perfectly, it could be stock airplane with minimum amount of 2D gauges, however custom/paid products will good to test as well. 

Aircraft

- List of CFG files being validated. If all of them presented - no actions required. If some/all except aircraft.cfg are absent - Process button available. When pressed, script will compare template CFGs (taken from MSFS generic plane) with aircrafts cfg and move sections in relative files. 
- Before proceed, important to check that all sections labeled properly - in format [SECTION_NAME] (without ";" before name, no text after or spaces inside of braces).
- If "unknown.cfg" file appear after processing - some of sections was not found in templates, first thing to do is check that it label written correctly.
- Original aircraft.cfg file will be renamed to ".aircraft.cfg" and ignored by JSON generator
- After processing, layout.json will be regenerated automatically

Engines

- Script checking values of critical parameters - currently engine_type and afterburner_available
- If checkbox checked near some of wrong parameter, they will be set to 0 after button pressed

Cockpit

- Script checking for instruments and gauges presence (not statuses, so if some gauge marked in green it still can be disabled)
- In red marked instruments that appear on external view so recommended to enable
- After button pressed, template values for each selected gauge will be inserted into the cockpit.cfg file, i.e. their values should be corrected manually

Systems

- List of lights listed here
- If some of them in FSX format, you can select them and press button below
- FSX has much shorter list of ligths compare to MSFS so possibly you see in the game not exactly the same effect
- You may need to adjust direction coordinates after conversion (by default set to 0,0,0)

Textures

- Experimental feature, haven't tested really: script display BMP textures that should be converted
- On convert button press, BMPs converted into DX5 flipped DDS
- Original BMPs are backed up by adding dot to their names

About

- Update availability, self update implemented but poorly tested
- If update available, tab label will be colored in red
- Manual update will open zip link in the browser
- Self update will pull update archive from the server, unpack it, replace EXEs and restart process



