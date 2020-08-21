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

The `master` branch was fully deleted on 2020-07-28.
If you get an error on pull on an old clone, that's why. The commands above should still work, though.