@REM Run after building INSTALL project. Only run this in the root directory of the repo or it will fail

set flag=""

IF "%~1"=="debug" (
	set flag=Debug
) ELSE IF "%~1"=="release" (
	set flag=Release
) ELSE (
	echo Pass in either debug or release
)

IF NOT %flag%=="" (
	cd build\x64\Debug\
	INSTALL ../../bin/x64/%flag%/UnityGGPO.dll ../../../Unity/Packages/UnityGGPO/Plugins/Windows/x86_64/UnityGGPO.dll
	INSTALL  ../../../../PlayFabParty/win32/PartySample/bin/x64/%flag%/PartySampleApp.dll ../../../Unity/Assets/PartySampleApp.dll
	INSTALL  ../../../../PlayFabParty/win32/PartySample/bin/x64/%flag%/PartyWin.dll ../../../Unity/Assets/PartyWin.dll
	cd ../../..
)