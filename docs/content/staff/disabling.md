# Disabling proxying in a channel
It's possible to block a channel from being used for proxying. To do so, use the `pk;serverconfig proxy blacklist` command. For example:

    pk;serverconfig proxy blacklist add #admin-channel #mod-channel #welcome
    pk;serverconfig proxy blacklist add all
    pk;serverconfig proxy blacklist remove #general-two
    pk;serverconfig proxy blacklist remove all
    
This requires you to have the *Manage Server* permission on the server. 