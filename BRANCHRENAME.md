On 2020-06-22, we renamed the `master` branch to `main`. 

This should mean very little, but if you have existing clones,
you'll need to update the branch references, like so:

1. **Fetch the latest branches from the remote:**  
`$ git fetch --all`
2. **Update the upstream remote's HEAD**  
`$ git remote set-head origin -a`
3. **Switch your local branch to track the new remote**  
`$ git branch --set-upstream-to origin/main`
4. **Rename your branch locally**  
`$ git branch -m master main`

(steps from https://dev.to/rhymu8354/git-renaming-the-master-branch-137b)

I'm going to re-branch `master` from `main`, leaving it at this notice's commit, and then delete in a week's time
so people have a chance to migrate. Hopefully this doesn't cause too much breakage. (if it does, do yell at me in the issues)