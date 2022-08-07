# SubCheck
A tool to automatically check some basic things on student submission files for the course Programming 4 at DAE

[![Build Status](https://github.com/avadae/subcheck/actions/workflows/dotnet.yml/badge.svg)](https://github.com/avadae/subcheck/actions)
[![GitHub Release](https://img.shields.io/github/v/release/avadae/subcheck?logo=github&sort=semver)](https://github.com/avadae/subcheck/releases/latest)

# Use
Drag a zip file containing your submission onto subcheck.exe
- A config.xml will be generated containing the default configuration, you might want to tweak this
- Your zip file will be unzipped and the contents analyzed.

Things that are checked (non exhaustive):
- Visual Studio 2022 is installed
- file is a zip file
- filename matches with the given regex (see config.xml)
- zip contains a vs2022 sln file
- zip contains only one sln file
- verifies that the folders are clean - no intermediate build output or .vs folders
- verifies that for each build target:
    - the platform toolset is v143 (vs2022)
    - the warning level is set to 4 (if not it changes that)
    - warnings are treated as errors (if not it changes that)
    - C++ Language Standard has been set to c++20
- if enabled, it will build all build targets
- if enabled, it will open the solution
