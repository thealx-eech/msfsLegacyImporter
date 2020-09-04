Init page

- Imported aircraft folder should be selected inside of Community folder. Perfectly, it could be stock airplane without manual changes, however custom/paid products will good to test as well. After folder is selected, available CFG files will be scanned and textures, relative tabs will appear.
- All further changes will be saved automatically, so if another aircraft will be selected - no changes will disappear.

Aircraft

- Validating list of CFG files. If all presented - no actions available. If some/all except aircraft.cfg are absent - Process button available. When pressed, script will compare template CFGs (taken from MSFS generic plane) with aircrafts cfg and move sections in relative files. 
- Important to check before process, that all sections labeled properly - in format [SECTION_NAME] (without ";" before name, not text after, lower case and spaces inside probably possible but not tested).
- If "unknown.cfg" file appear after processing - something wrong, first thing to check is section label. if it looks fine, I need example of processed files.
- original aircraft.cfg file will be renamed to ".aircraft.cfg" and will be ignored by JSON generator
- after processing, JSON regeneration is required (no implemented yet, planeconvertor can be used)

Engines

- script checking values of critical parameters - currently engine_type and afterburner_available
- if checkbox checked near some wrong values, they will be set to 0 after button pressed

Cockpit

- script checking for instruments and gauges present (not statuses, so if some gauge marked in green it still can be disabled)
- in red marked instruments that appear on external view
- after button press, template values for each gauge will be inserted into the cockpit.cfg file, i.e. they should be corrected manually

Textures

- experimental feature, haven't tested really: script display BMP textures that should be converted
- on convert button press, BMPs converted into DX5 flipped DDS
- original bmp backing up with adding dot to the name

About

- update availability, self update implemented but poorly tested
- if update available, tab label will be colored in red
- manual update will open zip link in the browser
- self update will get file by itself, unpack it, replace EXEs and reload


http://eech.online/msfslegacyimporter/msfsLegacyImporter_0.0.1.12.zip