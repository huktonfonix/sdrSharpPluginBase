# sdrSharpPluginBase 

Steps for getting a SDR# plugin to compile using SharpDevelop:

1. get sdrdev from sdr# plugin page
2. go to GITHUB and get SDRSHARP Common trunk
3. copy SDRSHARP Common trunk into <your development root>/sdrdev/SDRDEV/
4. copy ZoomFFT into a new folder and rename to your plugin
5. rename all ZoomFFT's to your plugin name
6. go through *.cs and rename the namespace to your plugin name
7. open SDRSharp.ZoomFFT.csproj and replace all occurrences of ZoomFFT with your plugin name
8. open SDRSharp.ZoomFFT and replace all occurrences of ZoomFFT with your plugin name
9. open SDRSharp.yourPlugin.csproj and copy the following 4 XML propertygroups: <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "> <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "> <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|x64'"> <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|x64'">
10. copy the block of propertygroups into SDRSharp.Common.csproj
11. copy the block of propertygroups into SDRSharp.Radio.csproj
12. copy the block of propertygroups into SDRSharp.Panview.csproj
13. go to the properties directory inside your project and open AssemblyInfo.cs. Edit to reflect new project name.
14. set compiling option to "allow unsafe code"
15. create ./bin/debug folders in your radio, common and panview folders; and then go to your SDR# install and copy SDRSharp.radio.dll, SDRsharp.panview.dll and SDRSharp.common.dll into them. DO NOT COMPILE radio using the project file. (a couple of interfaces are missing in the radio source code and compiling a fresh DLL won't work, so just copy them from a working install.) 
16. now build the project
17. go to /bin/debug/ and copy the DLL into your sdr# folder where the other DLLs reside. Edit "plugins" XML file and add your plugin.
18. start SDR# and the plugin should appear. If you've made a mistake in assembling the DLL, you will get an error. however, the error will be fairly easy to track to the problem. took me about 2 hours to get it right.
