# git-lfx
_Teaching git how to restore nuget packages_

`Lfx` is an adaption of [`Lfs`](https://git-lfs.github.com/). `Lfs` allows git to manage binary resources but requires a server be maintained to host the binaries. `Lfx` only supports restoring files already hosted either individually or as part of a zip archive (e.g. a `nuget` package).

# Quickstart
Before any of the following quickstart scenarios, install `git-lfx`:

1. Install [`chocolatey`](https://chocolatey.org/)
2. `choco install git-lfx`
3. relaunch `cmd.exe`
4. `git-lfx` _verify installation; dump help_

####Sync a repository and restore `lfx` pointers automatically:
1. `git lfx clone lfx-sample`
2. `git lfx files` _list files lfx downloaded_

####Sync a repository and restore `lfx` pointers manually:
1. `git clone lfx-sample`
2. `git lfx files` _list files `lfx` will download_
3. `type dls\tools\nuget.exe` _observe the binary content has been replaced with a `lfx` pointer_
4. `git lfx checkout` _restore binary content for `lfx` tracked files_
5. `dir dls\tools\nuget.exe` _observe the binary content is restored_ 

####Initialize a repository with `lfx` support:
1. `git lfx init`

####Add\Remove `lfx` filters to\from an existing repository:
1. `git init`
3. `git lfx config --list` _observe lack of `filter.lfx.*` config settings_ 
2. `git lfx config --set` _add `lfx` filter config settings_
3. `git lfx config --list` _observe addition of `filter.lfx.*` config settings_ 
4. _update `.gitattributes` to specify files using `lfx` filter_ (see below)
5. _add `.lfxconfig` to specify how how pointers are constructed as a function of file path_ (see below)
5. `git lfx config --unset`

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

## History
In the beginning, there was darkness, then light, then centralized source control systems (e.g. `TFS`) which stored all the dependencies necessary to build. Building was a simple matter of syncing the repository, build tools and all, and building. It was good. And worked well for those teams that could afford the time and energy of maintaining a TFS server. However, it didn't work so well for those who couldn't or for open source projects with thousands of loosely associated collaborators (e.g. Linux). They needed a different solution.

So, banished from centralized source control, along came distributed version control (e.g. `GIT`) which solved those pesky server maintenance issues by not having a central server at all. Instead the entire history of the repository was distributed to everyone. However, this solution came at with a cost. Distributing the history of every binary was prohibitively expensive and so the binaries had to be removed. And so, in this new world, a new step to restore the removed binaries was introduced after syncing the source but before building (e.g. `nuget restore`). It was ok. 

Naturally, efforts were made to hide the extra restore step and return to the utopic sync/build experience of centralized source control. Usually, this involved having the build preform the restore. However, this proved to be problematic. Some nuget packages included msbuild files which could not be restored before they were needed while, at the same time, modifying other msbuild project files which had already been processed. Yuck. And so restoring during the build was abandoned and the [official advice][3] became:

> when building from the command line, you need to run 'nuget restore' yourself before msbuild

Not exactly helpful for those trying to automate their build. So, what to do?

Binary files could not be stored in `GIT` however no one said we couldn't store _pointers_ to binary files. If `GIT` could recognize these pointers during a sync and download the referenced file then the build could proceed directly after the sync! And if `GIT` could replace binary files with a pointer before pushing changes then those pointers could be distributed instead of the binaries themselves. Wonderful! But how do we teach `GIT` to recognize and generate pointers?

# A Beautiful Hack
Stay tuned... 

# Actual Impetus
Builds composed of multipule C# projects often suffer from duplication of common project settings. Among other things, this makes enforcement of policy challenging (e.g. enforcing warnings as errors is enabled for all projects). Builds typically solve this problem by extracting common settings to a single location where they can be centrally administered (e.g. [coreclr][1] and [corefx][2] extract their common settings to a set of `dir.proj` files). 

Unfortunately, most teams roll their own variant of the `dir.proj` file solution as their build becomes larger and larger. Xamarin.Form libraries, however, are born needing dozens of projects in order to generate the zoo of binaries needed for each supported platform. In this case, it makes sense to invest in a generalized solution which can be packaged up and reused by Xamarin.Forms library authors. Nuget seemed natural distribution vehicle but executing a `nuget restore` to pull down msbuild files felt wrong. So `lfx`.

# License
The MIT License (MIT)
Copyright (c) 2016, Christopher E. S. King

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.


[1]: [https://github.com/dotnet/coreclr/blob/master/dir.props]
[2]: [https://github.com/dotnet/corefx/blob/master/dir.props]
[3]: [http://blog.davidebbo.com/2014/01/the-right-way-to-restore-nuget-packages.html]
[4]: [https://github.com/dotnet/corefx/blob/master/dir.props]
