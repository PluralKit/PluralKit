Currently viewable here! https://pk-webs-beta.spectralitree.com/

# pk-webs: beta

This repo is a complete rewrite of [pk-webs](https://github.com/Spectralitree/pk-webs). Written in svelte instead of react.

pk-webs uses [PluralKit](https://pluralkit.me/)'s api to display, members and groups registered on PluralKit. You can also supply your system's token to edit your own data.

This is a third-party application, and not officially supported by the PluralKit devs.

If you encounter a bug, please either open an issue, or bring it up in the PluralKit support server (which has a channel for community resources like this one)

## Technology
This project is built using [Vite](https://vitejs.dev/), using the svelte-ts template.

Some of the other stuff used to get this working:
* sveltestrap (https://sveltestrap.js.org/)
* svelte-navigator (https://github.com/mefechoel/svelte-navigator)
* svelte-toggle (https://github.com/metonym/svelte-toggle)
* svelecte (https://mskocik.github.io/svelecte/)
* svelte-icons (https://github.com/Introvertuous/svelte-icons)
* discord-markdown (https://github.com/brussell98/discord-markdown)
* moment (https://momentjs.com/)

The code used to make routing work on github pages can be found [here](https://github.com/rafgraph/spa-github-pages)

## Contributing
Feel free to contribute! This repo does not have any format for issues or pull requests, so just go ahead and open one. I tend to not immediately notice whenever new issues/PRs are opened, so expect a couple days of delay (or poke me in PluralKit's official discord!)

## Privacy
pk-webs is a static web-app. It only saves your token, settings and other small things it needs to keep track of in your localstorage. We do not have access to your token, and it does not use this token for anything other than what *you* tell it to do.

That being said, there might be bugs, and there's no guarantee everything will work correctly! Again, if you find a bug, do let us know.