@REM Run after building INSTALL project. Only run this in the root directory of the repo or it will fail
cd build\x64\Debug\
INSTALL ../../bin/x64/Debug/UnityGGPO.dll ../../../Unity/Packages/UnityGGPO/Plugins/Windows/x86_64/UnityGGPO.dll
cd ../../..
