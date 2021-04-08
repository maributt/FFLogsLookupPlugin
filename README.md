<h1 align="center">FFLogsLookup</h1>
<p align="center">A <a href="https://github.com/goatcorp/Dalamud">Dalamud</a> plugin to display an inspected character's parses directly in the inspect window!<br>install for a brief setup tutorial & type <code>/ffll</code> in-game to configure!</p><br>

<img src="https://user-images.githubusercontent.com/76499752/113009205-09b6a580-9178-11eb-8942-088ec0a8528a.png" width="40%" align="right">

This plugin aims to provide a simple way of browsing
an inspected character's parse records from the [FFLogs](https://www.fflogs.com/) website.
By displaying the current tier's percentiles achieved by the character
below their character portrait, the user can get a quick glimpse of another player's performance.

Should the character not have have an FFLogs userpage tied
to their username, the display will be adjusted accordingly-
the same applies for if a character's parses have been hidden.

Additionally, the plugin can be configured to look and feel
more personal, allowing for a X and Y offset to be specified
through the configuration menu accessible via the command `/ffll`,
normal parses can also be defaulted to if no savage parses are found.
Should this be the case, a `Normal` label will be displayed underneath
the parse entries in the inspect window.

You can also click the parses to open a link to the character's FFLogs page!

<br>
<br>


## Roadmap

- **Clean** up the code because it's getting messy very fast
- **Add** a way for the player to choose the rendering method of the percentiles
(currently the ImGui is causing some problems where other game windows will not be drawn *over* the percentiles and that's ugly and annoying)
  - Between ImGui and in-game rendering (by injecting AtkTextNodes into the inspect window directly)
- **Add** a way to check whether or not an inspect character's clears were bought or not (not 100% accurate as clears could also simply not be logged, but that's sus)
- **Add** support/testing for more tiers in the tier selector
- **Change** the way the tier selector looks and is used (it's gonna get cramped fast if I add more tiers in there)
- **Add** command aliases to toggle on/off config options without having to use the config window (so that it can be put in a macro for ease of access)
