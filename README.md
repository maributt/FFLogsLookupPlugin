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


## Planned features

- **Add** a way to manually select which raid tier to fetch parses from (currently it pulls only from the latest)
- **Add** a way to display a user's ultimate raid parses if requested (separately from the normal entries / or not)
