# Roles and permissions

PluralKit requires some channel permissions in order to function properly:

- *Everything* PluralKit does aside from the Message Info app command requires **View Channel** permissions in a channel. 
- Message proxying requires the **Manage Messages**, **Manage Webhooks**, and **Send Messages** permissions in a channel.
- Most commands require the **Embed Links** and **Add Reactions** permissions to function properly.
  - Commands with reaction menus also require **Manage Messages** to remove reactions after clicking.
  - Commands executed via reactions (for example the :x:, :bell:, and :question: reactions, as well as any commands with reaction menus) need **Read Message History** to be able to see that reactions were added. 
  - A couple commands (`pk;s color` and `pk;m <name> color`) currently require **Attach Files**. 
- [Proxy logging](/staff/logging) requires the **Send Messages** permission in the log channel.
- [Log cleanup](/staff/compatibility/#log-cleanup) requires the **Manage Messages** permission in the log channels.

## Webhook permissions
Webhooks exist outside of the normal Discord permissions system, but as of August 2022 they mostly follow the permissions of the webhook owner (in this case, PluralKit).

PluralKit will also make an attempt to apply the sender account's permissions to proxied messages. For example, role mentions, `@everyone`, and `@here`
will only function if the sender account has that permission. The same applies to link embeds.

For external emojis to work in proxied messages, PluralKit or one of its roles must have the "Use External Emojis" permission. If it still doesn't work, 
check if the permission was denied in channel-specific permission settings. PluralKit must also be in the server the external emoji belongs to.

## Troubleshooting

### Permission checker command
To quickly check if PluralKit is missing channel permissions, you can use the `pk;debug permissions` command in the server
in question. It'll return a list of channels on the server with missing permissions. This may include channels
you don't want PluralKit to have access to for one reason or another (eg. admin channels).

If you want to check permissions in DMs, you'll need to add a server ID, and run the command with that.
For example:

    pk;debug permissions 466707357099884544
    
You can find this ID [by enabling Developer Mode and right-clicking (or long-pressing) on the server icon](https://discordia.me/developer-mode).
