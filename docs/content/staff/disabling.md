# Disabling proxying in a channel
It's possible to block a channel from being used for proxying. To do so, use the `pk;blacklist` command. For example:

    pk;serverconfig blacklist add #admin-channel #mod-channel #welcome
    pk;serverconfig blacklist add all
    pk;serverconfig blacklist remove #general-two
    pk;serverconfig blacklist remove all
    
This requires you to have the *Manage Server* permission on the server. 