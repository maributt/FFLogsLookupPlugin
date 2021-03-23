# FFLogsLookupPlugin
a Dalamud plugin that displays players' percentiles in the character inspect window

this is very unfinished but should work just fine as-is if you put a bearer token that you get yourself from the fflogs v2 api (i'll add a thing to do it automatically from a client id/client secret pair later and add a lil tutorial for it as well)

planned features:
- add a config UI for display offsets and general configuration (if the user wants the parses displayed below the character inspect window instead for example)
- make it so the plugin requests a bearer token automatically for the api calls instead of having to provide it manually (for now)
- make viewing normal raid parses possible / smoother (it should work atm! but untested) 
- fix the imgui hovertext stuff because for some reason it doesn't work???? idk (maybe scrap the idea if it's too janky)
- make the display of percentiles more uniform (currently they're just ImGui Texts being put one after another, so if for instance you have five consecutive 100 parses ((you absolute champion)) it would display kind of weird (currently I replace 100s with ★s just because 3 digit parses will look very weird without predetermined X positions for texts)

![ffxiv_dx11_20210323164009451](https://user-images.githubusercontent.com/76499752/112174147-75d45f00-8bf6-11eb-9b2a-3989f5e52082.png)
