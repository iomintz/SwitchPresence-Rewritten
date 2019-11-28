# SwitchPresence-Rewritten
[![ko-fi](https://www.ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/X8X0LUTH) \
Change your Discord rich presence to your currently playing Nintendo Switch game! Concept taken from [SwitchPresence](https://github.com/Random0666/SwitchPresence) by [Random](https://github.com/Random0666)

## Setup
[Soft-mod your Switch first](https://switch.homebrew.guide/).

Then create an application at the [Discord Developer Portal](https://discordapp.com/developers/applications/).
Call your application `Nintendo Switch` or whatever you would like.

Then create a file called Config.json in the same folder as SwitchPresence-Rewritten.exe:

```json
{
    "IPOrMacAddress": "10.0.0.2",
    "ClientID": 1234,
    "BigImageKey": "",
    "BigImageText": "",
    "SmallImageKey": "",
    "StateText": "",
    "ShowTimeElapsed": true
}
```

Only the `IPOrMacAddress` and `ClientID` fields are required.

You can also optionally dump game icons using a helper homebrew included in releases it will also give you the option to toggle the SwitchPresence sysmodule!<br>
After you have dumped the icons you can bulk upload them to your Discord Developer Application under __Rich Presence → Art Assets__.
You can upload them with the name given to them on dump, or optionally upload your own icon and set the SwitchPresence client to load that icon using the name of the custom icon (BigImageKey).

## Technical Info

The protocol for the sysmodule is a very simple struct sent via TCP \
```
struct titlepacket
{
    u32 magic; //Padded to 8 bytes by the compiler
    u64 tid;
    char name[512];
};
```
**Please note that magic is padded to 8 bytes which can be read into a u64 if wanted**<br>
The Packet is sent about every 5 seconds to the client from the server (in this case the switch).<br>
If a client is not connect it will not send anything.<br>
