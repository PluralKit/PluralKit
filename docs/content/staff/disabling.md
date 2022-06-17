# Disabling proxying in a channel
It's possible to block a channel from being used for proxying. To do so, use the `sp;blacklist` command. For example:

    sp;blacklist add #admin-channel #mod-channel #welcome
    sp;blacklist add all
    sp;blacklist remove #general-two
    sp;blacklist remove all
    
This requires you to have the *Manage Server* permission on the server. 