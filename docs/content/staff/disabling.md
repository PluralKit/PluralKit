# Disabling proxying in a channel
It's possible to block a channel from being used for proxying. To do so, use the `pk;blacklist` command. For example:

    pk;blacklist add #admin-channel #mod-channel #welcome
    pk;blacklist add all
    pk;blacklist remove #general-two
    pk;blacklist remove all
    
This requires you to have the *Manage Server* permission on the server. 