# PluralKit "application command" helpers

## Adding new commands

Edit the `COMMAND_LIST` global in `commands.py`, making sure that any
command names that are specified in that file match up with the
command names used in the bot code (which will generally be in the list
in `PluralKit.Bot/ApplicationCommandMeta/ApplicationCommandList.cs`).

TODO: add helpers for slash commands to this

## Dumping application command JSON

Run `python3 commands.py` to get a JSON dump of the available application
commands - this is in a format that can be sent to Discord as a `PUT` to
`/applications/{clientId}/commands`.

## Updating Discord's list of application commands

From the root of the repository (where your `pluralkit.conf` resides),
run `python3 ./scripts/app-commands/update.py`. This will **REPLACE**
any existing application commands that Discord knows about, with the
updated list.