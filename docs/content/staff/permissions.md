# Roles and permissions

PluralKit requires some channel permissions in order to function properly:

- Message proxying requires the **Manage Messages** and **Manage Webhooks** permissions in a channel.
- Most commands require the **Embed Links**, **Attach Files** and **Add Reactions** permissions to function properly.
  - Commands with reaction menus also require **Manage Messages** to remove reactions after clicking.
- [Proxy logging](./logging.md) requires the **Send Messages** permission in the log channel.
- [Log cleanup](./compatibility.md#log-cleanup) requires the **Manage Messages** permission in the log channels.

Denying the **Send Messages** permission will *not* stop the bot from proxying, although it will prevent it from sending command responses. Denying the **Read Messages** permission will, as any other bot, prevent the bot from interacting in that channel at all.

## Webhook permissions
Webhooks exist outside of the normal Discord permissions system, and (with a few exceptions) it's not possible to modify their permissions.

However, PluralKit will make an attempt to apply the sender account's permissions to proxied messages. For example, role mentions, `@everyone`, and `@here`
will only function if the sender account has that permission. The same applies to link embeds.

For external emojis to work in proxied messages, the `@everyone` role must have the "Use External Emojis" permission. If it still doesn't work, check if the permission was denied in channel-specific permission settings.

## Troubleshooting

### Permission checker command
To quickly check if PluralKit is missing channel permissions, you can use the `pk;permcheck` command in the server
in question. It'll return a list of channels on the server with missing permissions. This may include channels
you don't want PluralKit to have access to for one reason or another (eg. admin channels).

If you want to check permissions in DMs, you'll need to add a server ID, and run the command with that.
For example:

    pk;permcheck 466707357099884544
    
You can find this ID [by enabling Developer Mode and right-clicking (or long-pressing) on the server icon](https://discordia.me/developer-mode).