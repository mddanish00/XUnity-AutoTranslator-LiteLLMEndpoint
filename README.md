# XUnity-AutoTranslator-LiteLLMEndpoint

LLM-backed (by [LiteLLM](https://github.com/BerriAI/litellm)) translator endpoint for [XUnity.Auto.Translator](https://github.com/bbepis/XUnity.AutoTranslator).

Mainly tested on [PriconneRe-TL](https://github.com/ImaterialC/PriconneRe-TL).

## Requirements
- XUnity-AutoTranslator v5.0.0 or higher.
- LiteLLM installed using uv or pipx.

## Installation

1. Install XUnity-AutoTranslator if you not done yet. Please check the project [README.md](https://github.com/bbepis/XUnity.AutoTranslator?tab=readme-ov-file#installation) for instructions.

2. Download pre-compiled dll from Releases and install into `XUnity.AutoTranslator\Translators` folder. (The folder location may different based on loader you are using to use for XUAT)

3. Copy the `SampleConfig/LiteLLM` folder to your loader config folder. For example, for BepInEx, in `BepInEx/config`.

> DLL Release is compiled with the pinned version of XUnity-AutoTranslator. (Usually, whatever is the latest version at the time of commit.)

4. Run your game once to generate/update the XUAT configuration file. Once the game has run and initialized properly, exit the game.

5. Backup your XUAT configuration file (`AutoTranslatorConfig.ini`). After you have a backup copy, edit the configuration and change the `Endpoint` setting to `LiteLLM`.  Your `[Service]` section should look like this:
```
[Service]
Endpoint=LiteLLM
FallbackEndpoint=
```

## Configuration

Example config in `AutoTranslatorConfig.ini`.

```
[LiteLLM]
ServerPort=4000
BinaryPath=C:\Users\User\.local\bin\litellm.exe
ExtraArguments=
ConfigPath=C:\Users\User\Desktop\litellm-config.yaml
ApiKey=sk-67696769
ModelName=gpt-oss-20b
ModelParams=temperature=0.2;top_p=0.9;frequency_penalty=0;presence_penalty=0
UseExternalServer=False
LogServerMessages=False
```

- ServerPort: Port used to launch and communicate with the server.
- BinaryPath: Path to `litellm` binary.
- ExtraArguments: Extra arguments to be passed to `litellm` binary when launching the server.
- ConfigPath: Path to a LiteLLM config.
- ApiKey: Api Key to access the server. Either LiteLLM Master Key or any of user-created Virtual Keys.
- ModelName: Model to use for translation.
- ModelParams: Model customization for this translation task.
- UseExternalServer: Use external LiteLLM Proxy server instead.
- LogServerMessages: Log LiteLLM server message in XUAT console.

## Building

For developers, it is recommended to use your project XUnity-AutoTranslator DLL to build this project as long it meet the requirements for maximum compatibility.

- Make sure latest .NET SDK is installed.
- XUnity-AutoTranslator v5.0.0 or higher. 
- Download XUnity.AutoTranslator-Developer-X.X.X.zip. 
- If you using IL2CPP, download XUnity.AutoTranslator-Developer-IL2CPP-X.X.X.zip instead.
- If you want to use your project DLLs, put DLLs in libs folder or libs-il2cpp if using il2cpp.


### Normal Version

1. Unzip XUnity.AutoTranslator-Developer-X.X.X.zip in this repo. Rename `Developer` folder to `libs`.
2. Run the build command.
```
dotnet build -c Release -v n -f net35
```
3. Copy the builded DLL, `LiteLLMEndpoint\bin\Release\net35\LiteLLM.dll` to your project `XUnity.AutoTranslator\Translators` folder.

### IL2CPP Version


1. Unzip XUnity.AutoTranslator-Developer-IL2CPP-X.X.X.zip in this repo. Rename `Developer` folder to `libs-il2cpp`.
2. Run the build command.
```
dotnet build -c Release -v n -f net6.0
```
3. Copy the builded DLL, `LiteLLMEndpoint\bin\Release\net6.0\LiteLLM.dll` to your project `XUnity.AutoTranslator\Translators` folder.

## License

This project is licensed under the [MIT license](./LICENSE).

Some of this project code are based on [XUnity.AutoTranslate.LlmTranslators
](https://github.com/joshfreitas1984/XUnity.AutoTranslate.LlmTranslators) project, licensed under [MIT](https://raw.githubusercontent.com/joshfreitas1984/XUnity.AutoTranslate.LlmTranslators/refs/heads/master/LICENSE) and [XUnity-AutoTranslator-StasServerEndpoint](https://github.com/mddanish00/XUnity-AutoTranslator-StasServerEndpoint) project, licensed under [MIT](https://raw.githubusercontent.com/mddanish00/XUnity-AutoTranslator-StasServerEndpoint/refs/heads/main/LICENSE) which is a fork of [XUnity-AutoTranslator-SugoiOfflineTranslatorEndpoint](https://github.com/Vin-meido/XUnity-AutoTranslator-SugoiOfflineTranslatorEndpoint) project.

## Acknowledgement

- Thanks to [joshfreitas1984](https://github.com/joshfreitas1984) for [XUnity.AutoTranslate.LlmTranslators
](https://github.com/joshfreitas1984/XUnity.AutoTranslate.LlmTranslators).
- Thanks to [Vin-meido](https://github.com/Vin-meido) for [XUnity-AutoTranslator-SugoiOfflineTranslatorEndpoint](https://github.com/Vin-meido/XUnity-AutoTranslator-SugoiOfflineTranslatorEndpoint).
