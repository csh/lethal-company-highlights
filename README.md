# Lethal Company Death Capture

Uses Steam's new Game Recording feature to capture your best deaths in service to the Company.

# Requirements

You **must** manually [enable](https://help.steampowered.com/en/faqs/view/23B7-49AD-4A28-9590#6) **Record in Background** in the Steam client.

Set the quality settings to your preference, I use a 60 minute buffer at "High" quality.

That's it, you're done.

## Recommendations

You should also turn on **Record Microphone** so that Steam will capture the despair as another Coil Head jumps out of nowhere for another sneak attack.

**Automatic Gain Control** may be useful but can produce crackly audio depending on your setup. If you plan to use this to capture clips long term, I recommend jumping into a save solo and testing with AGC on/off - this is the best we've got until Steam adds manual gain control.

## Configuration

[LethalConfig](https://thunderstore.io/c/lethal-company/p/AinaVT/LethalConfig/) is used for configuring the mod, all options are realtime and do not require restarting the client to reflect the change.

By default, the Steam overlay will open after you die; there is a short delay to facilitate a more natural reaction to whatever caused your demise. 

You can disable this behavior in the configuration menu if desired, just remember that if your recording length is low you will need to check and export any clips you wish to keep at some point.

This mod will also clip other people's deaths under certain conditions:
- You watched the other person die in spectator mode.
- You were in the immediate vicinity of a player who just died.
- You saw a person that died within the last 10 seconds.

See the [v0.1.5](https://github.com/csh/lethal-company-highlights/releases/tag/v0.1.5) release notes for more information on how these work.

## Capture Modes

There are two capture modes presently, I couldn't personally settle on which I preferred for clipping and so I included both options the Steam Timeline API currently supports.

**"Clip"** mode will create a gold line underneath the section of the recording where somebody died on the recording overlay, this allows for easier one-click clipping. **This is the default for now**, though unfortunately it doesn't seem like the death cause is visible for this mode.

![Clip Mode](https://github.com/user-attachments/assets/715b9be6-0f79-401a-b077-3291e4ab542c)

**"Marker"** mode will create a little skull icon on the recording overlay, this is a bit easier to click on at the time of writing but does only captures a brief instant and thus you will need to adjust duration manually.

![Marker Mode](https://github.com/user-attachments/assets/ce860967-4794-4593-ab02-4d548fb57ffc)

## Searchable Clips

Additional metadata has been added to recordings to help you sift through the best bits, this includes the following information:

- The name of the planet you're exploring, as of v0.1.5 this has replaced the tooltip on the timeline.
  - The `Orbiting <planet name>` tooltip is still present before landing.
- Who participated in a round.
- Who died during a round, letting you finally catch out that one friend that never dies!

![Capture Metadata](https://github.com/user-attachments/assets/9fb3eaf4-29c0-433d-89ae-26b51b380b63)

## Other Features

Integration with Coroner attempts to use the same death message you see at the end of the level in the recording tooltip.

At the time of writing, the recording bar changes colour based on the state of the game too to indicate if, in a recording, you're in the menu, on the ship or exploring.

As Steam adjusts the "Timeline" API I will look into adding more detail.
