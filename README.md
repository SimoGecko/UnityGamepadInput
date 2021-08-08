# Easy Gamepad Input for Unity
Provides a simple way to access gamepad input in Unity across a wide variety of gamepad and controller types and platforms


## Sample Usage

```c#
GamepadInfo info = new GamepadInfo(id: 1, GamepadType.Xbox360);

bool    buttonValue  = GamepadInput.GetButton(GamepadButton.Button_South, info);
float   axisValue    = GamepadInput.GetAxis(GamepadAxis.DPadX, info);
Vector2 stickValue   = GamepadInput.GetStick(GamepadStick.LeftStick, info);

```

## Features
- Simple to use interface that mimicks Unity's `Input.GetKey(...)` and `Input.GetMouseButton()`
- Get the input from all controllers without having to worry about mappings, platform or Unity's built-in buttons and axes indices
- Get input from any controller or just a specific one
- Supports all popular controllers, including *Xbox 360, Xbox One, Xbox SeriesX/S, PS2, PS3, PS4, PS5, Steam Controller, Generic Pc Gamepads, Nintendo Switch Pro Controller,* and *Nintendo Joycons*
- Customize the mappings and add new controller types
- No need to use Unity new input System
- No need to worry about different controllers on different platforms all mapping to different values

## Setup
1. Copy `GamepadInput.cs` and `controller_mappings_data.csv` in your project under `Assets/`
2. Create a new GameObject, assign `GamepadInput.cs` to it
3. Load the mappings by dragging `controller_mappings_data.csv` in the `Mappings Data` field of the script, then right-clicking the script header and selecting `Apply Mappings`. This will create all controller mappings that you can inspect and modify.
4. Add the content of `InputManager.txt` to the end of your Input Manager found at `ProjectSettings/InputManager.asset` (you might need to restart Unity for the changes to take effect)
5. Done!
6. Start using any gamepad input in your scripts, or test the input using the code found in `GamepadTest.cs`


## Interface

```c#
    // PURPOSE: Returns true while the button is pressed
    bool GamepadInput.GetButton(GamepadButton button, GamepadInfo info);
    
    // PURPOSE: Returns true when the button was pressed this frame
    bool GamepadInput.GetButtonDown(GamepadButton button, GamepadInfo info);
    
    // PURPOSE: Returns true when the button was released this frame
    bool GamepadInput.GetButtonUp(GamepadButton button, GamepadInfo info);
    
    // PURPOSE: Returns the value of a given axis
    // NOTE: range [-1, 1] for LeftX/Y, RightX/Y, DPadX/Y, range [0, 1] for Left/RightTrigger
    float GamepadInput.GetAxis(GamepadAxis axis, GamepadInfo info);

    // PURPOSE: Returns the value of a given stick
    // NOTE: values in the range [-1, 1] in both dimensions
    Vector2 GamepadInput.GetStick(GamepadStick stick, GamepadInfo info);

```

## Documentation
The GamepadInfo structure is used to specify which controller you are interested in, with an index (0 = all controllers, 1-4 = specific controller) and a type that is used to find the correct mapping.