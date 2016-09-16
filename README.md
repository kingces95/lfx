# git-lfx
Teaching git how to restore nuget packages.

## Quickstart
Before any of the following quickstart scenarios, install `git-lfx`:

1. Install [`chocolatey`](https://chocolatey.org/)
2. `choco install git-lfx`
3. relaunch `cmd.exe`
4. `git-lfx` # should dump help

####Sync a reposotiry with `lfx` pointers:
1. `git lfx clone <url>`

####Initialize a repository with `lfx` support:
1. `git lfx init`

####Add\Remove `lfx` filters to an existing repository:
1. `git lfx config --set` 
2. `git lfx config --list`
3. update\add `.gitattributes` and `.lfxconfig`, see below for examples
3. `git lfx config --unset`

####Initialize a repository with `lfx` support + samples + exploration:
1. `git lfx init --samples`
2. `git lfx env`
3. `type dls\tools\NuGet.exe`
4. `git lfx smudge dls\tools\NuGet.exe > %TEMP%\Nuget.exe` _resolve binary content given `lfx` pointer_
5. `git lfx checkout` _smudge filter replaces `dls\tools\nuget.exe` `lfx` pointer with binary content_
6. `dls\tools\NuGet.exe restore dls\packages\packages.config -PackagesDirectory dls\packages`
7. `git lfx clean dls\packages\NUnit.2.6.4\lib\nunit.framework.dll` _generate `lfx` pointer given binary_
8. `git add .` _clean filter converts nuget package file content to `lfx` pointers_
9. `git lfx show dls\packages\NUnit.2.6.4\lib\nunit.framework.dll` _dump example of content actually staged for commit_
10. `git commit -m "Add nunit v2.6.4"` _commit `lfx` pointers_

## History (The long version)
In the beginning, there was darkness, then light, then centralized source control systems (e.g. `TFS`) which stored all the dependencies necessary to build. Building was a simple matter of syncing the repository, build tools and all, and building. It was good. And worked well for those teams that could afford the time and energy of maintaining a TFS server. However, it didn't work so well for those who couldn't or for open source projects with thousands of loosly associated collaborators (e.g. Linux). They needed a different solution.

So, banished from centralized source control, along came distributed version control (e.g. `GIT`) which solved those pesky server maintainance issues by not having a central server at all. Instead the entire history of the repository was distributed to everyone. However, this solution came at with a cost. Distributing the history of every binary was prohibitively expensive and so the binaries had to be removed. And so, in this new world, a new step to restore the removed binaries was introduced after syncing the source but before building (e.g. `nuget restore`). It was ok. 

Naturally, efforts were made to hide the extra restore step and return to the utopic sync/build experiance of centralized source control. Usually, this involved having the build preform the restore. However, this proved to be problematic. Some nuget packages included msbuild files which could not be restored before they were needed while, at the same time, modifying other msbuild project files which had already been processed. Yuck. And so restoring during the build was abandonded and the [official advice][3] became:

> when building from the command line, you need to run 'nuget restore' yourself before msbuild

Not exactly helpful for those trying to automate their build. So, what to do?

Binary files could not be stored in `GIT` however no one said we couldn't store _pointers_ to binary files. If `GIT` could recognize these pointers during a sync and and download the referenced file then the build could proceed directly after the sync! And if `GIT` could replace binary files with a pointer before pushing changes then those pointers could be distributed instead of the binaries themselves. Wonderful! But how do we teach `GIT` to recognize and generate pointers?

# A Beautiful Hack
Stay tuned... 

# Actual Impetus
Builds composed of multipule C# projects often suffer from duplication of common project settings. Umong other things, this makes enformcement of policy challenging (e.g. enforcing warnings as errors is enabled for all projects). Builds typically solve this problem by extracting common setttings to a single location where they can be centrally adiministered (e.g. [coreclr][1] and [corefx][2] extract their common settings to a set of `dir.proj` files). Unfortunetly, most teams roll their own variant of this solution as their builds become larger and larger. Xamarin.Form libraries, however, are born needed dozens of projects in order to generate the zoo of binaries needed for each supported platform. And so, in this case, it makes sense to invest in a generalized solution which can be packaged up and reused by Xamarin.Forms library authors. Nuget seemed natrual distribution vehicle except executing a `nuget restore` to pull down msbuild files felt wrong so `lfx`.

# License
The MIT License (MIT)
Copyright (c) 2016, Christopher E. S. King

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.


[1]: [https://github.com/dotnet/coreclr/blob/master/dir.props]
[2]: [https://github.com/dotnet/corefx/blob/master/dir.props]
[3]: [http://blog.davidebbo.com/2014/01/the-right-way-to-restore-nuget-packages.html]
