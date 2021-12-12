var ghpages = require('gh-pages');

ghpages.publish(
    'public', // path to public directory
    {
        branch: 'gh-pages',
        repo: 'https://github.com/Spectralitree/pk-webs-svelte.git', // Update to point to your repository  
        user: {
            name: 'Spectralitree', // update to use your name
            email: 'spectralitree@gmail.com' // Update to use your email
        }
    },
    () => {
        console.log('Deploy Complete!')
    }
)