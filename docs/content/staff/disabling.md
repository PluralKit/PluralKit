# Disabling bot functionality
You can use the blacklist commands to disable proxying or text commands in some channels of your server. [You can also disable PluralKit by taking away its permissions.](/staff/permissions)

## Disabling proxying in a channel
It's possible to block a channel from being used for proxying. To do so, use the `pk;serverconfig proxy blacklist` command. For example:

    pk;serverconfig proxy blacklist add #admin-channel #mod-channel #welcome
    pk;serverconfig proxy blacklist add all
    pk;serverconfig proxy blacklist remove #general-two
    pk;serverconfig proxy blacklist remove all
    
This requires you to have the *Manage Server* permission on the server. 

## Disabling commands in a channel
It's possible to block a channel from being used for text commands. To do so, use the `pk;serverconfig command blacklist` command. For example:

    pk;serverconfig command blacklist add #admin-channel #mod-channel #welcome
    pk;serverconfig command blacklist add all
    pk;serverconfig command blacklist remove #general-two
    pk;serverconfig command blacklist remove all
    
This requires you to have the *Manage Server* permission on the server. If you have the *Manage Server* permission on the server you **will not be affected by the command blacklist** and will always be able to run commands.