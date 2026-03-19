	@echo off
	set WORKSPACE=..\..\
	set LUBAN_DLL=%WORKSPACE%\Config\Tools\Luban\Luban.dll
	set CONF_ROOT=.
	dotnet %LUBAN_DLL% ^
	    -t all ^
	    -c cs-simple-json ^
	    -d bin ^
	    -d json ^
	    --conf %CONF_ROOT%\luban.conf ^
	    -x outputCodeDir=%WORKSPACE%\Assets\Configs\Generated\Code^
	    -x outputDataDir=%WORKSPACE%\Assets\Configs\Generated\Data
	pause