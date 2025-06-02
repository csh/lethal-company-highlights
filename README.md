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

By default, the Steam overlay will open after a death occurs; there is a short delay to facilitate a more natural reaction to whatever caused your demise. 

You can disable this behavior in the configuration menu if desired, just remember that if your recording length is low you will need to check and export any clips you wish to keep at some point.

## Capture Modes

There are two capture modes presently, I couldn't personally settle on which I preferred for clipping and so I included both options the Steam Timeline API currently supports.

**"Clip"** mode will create a gold line underneath the section of the recording where somebody died on the recording overlay, this allows for easier one-click clipping. **This is the default for now**, though unfortunately it doesn't seem like the death cause is visible for this mode.

![Clip Mode](https://github.com/user-attachments/assets/715b9be6-0f79-401a-b077-3291e4ab542c)

**"Marker"** mode will create a little skull icon on the recording overlay, this is a bit easier to click on at the time of writing but does only captures a brief instant and thus you will need to adjust duration manually.

![Marker Mode](https://github.com/user-attachments/assets/ce860967-4794-4593-ab02-4d548fb57ffc)

## Other Features

Integration with Coroner attempts to use the same death message you see at the end of the level in the recording tooltip.

Recordings are labelled based on the state of the game:
- Orbiting [planet name]
- Exploring [planet name]

At the time of writing, the recording bar changes colour based on the state of the game too to indicate if, in a recording, you're in the menu, on the ship or exploring.

As Steam adjusts the "Timeline" API I will look into adding more detail.
