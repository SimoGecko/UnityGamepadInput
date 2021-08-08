// (c) Simone Guggiari 2021

using System.Collections.Generic;
using UnityEngine;

public enum GamepadType
{
    Xbox360, XboxOne, XboxSeries,
    PS2, PS3, PS4, PS5,
    SteamController, PcGamepad,
    SwitchPro, SwitchJoyconL, SwitchJoyconR,
    COUNT, INVALID
};

public enum GamepadPlatform
{
    Windows, MacOS, Linux,
    COUNT, INVALID
}

public enum GamepadStick
{
    LeftStick, RightStick, DPad,
    COUNT, INVALID
};

public enum GamepadAxis
{
    LeftX, LeftY,
    RightX, RightY,
    DPadX, DPadY,
    LeftTrigger, RightTrigger,
    COUNT, INVALID
}

public enum GamepadButton
{
    Button_South, Button_East, Button_West, Button_North,
    DPad_Up, DPad_Down, DPad_Left, DPad_Right,
    LeftShoulder, LeftTrigger, LeftStick,
    RightShoulder, RightTrigger, RightStick,
    Back, Start, Center, Special,
    LeftStick_Up, LeftStick_Down, LeftStick_Left, LeftStick_Right,
    RightStick_Up, RightStick_Down, RightStick_Left, RightStick_Right,
    COUNT, INVALID
}

[System.Serializable]
public struct GamepadInfo
{
    public int id;
    public GamepadType type;

    public GamepadInfo(int id, GamepadType type)
    {
        this.id = id;
        this.type = type;
    }
}

////////// PURPOSE: Abstraction to have different gamepads work all via the same interface //////////
public class GamepadInput : MonoBehaviour
{
    // -------------------- VARIABLES --------------------

    [SerializeField] private GamepadPlatform currentPlatform = GamepadPlatform.Windows;
    [SerializeField] private PlatformMappings[] mappings;
    [SerializeField] private TextAsset mappingsData;

    private GamepadRawData[] gamepadRawData = null;
    private GamepadRawData[] oldGamepadRawData = null;
    private static bool updatedThisFrame = false;

    const float axisPressThreshold = 0.3f;

    // -------------------- BASE METHODS --------------------

    private void Awake()
    {
        oldGamepadRawData = ReadAllRawData();
        gamepadRawData = ReadAllRawData();
    }

    private void PreUpdate()
    {
        updatedThisFrame = true;
        oldGamepadRawData = gamepadRawData;
        gamepadRawData = ReadAllRawData();
    }

    private void LateUpdate()
    {
        updatedThisFrame = false;
    }

    // -------------------- CUSTOM METHODS --------------------


    #region INTERFACE
    // PURPOSE: Returns true while the button is pressed
    public static bool GetButton(GamepadButton button, GamepadInfo gamepadInfo)
    {
        if (!IsValidGamepadId(gamepadInfo.id)) return false;
        CheckPreUpdate();

        GamepadMapping mapping = GetMapping(gamepadInfo, CurrentPlatform);
        GamepadRawData data = GetRawData(gamepadInfo);
        int buttonIndex = mapping.GetButtonIndex(button);
        if (0 <= buttonIndex && buttonIndex < data.buttons.Length)
        {
            return data.buttons[buttonIndex];
        }
        else
        {
            return TryObtainButtonFromAxisValue(button, mapping, data);
        }
    }

    // PURPOSE: Returns true when the button was pressed this frame
    public static bool GetButtonDown(GamepadButton button, GamepadInfo gamepadInfo)
    {
        if (!IsValidGamepadId(gamepadInfo.id)) return false;
        CheckPreUpdate();

        GamepadMapping mapping = GetMapping(gamepadInfo, CurrentPlatform);
        GamepadRawData newData = GetRawData(gamepadInfo);
        GamepadRawData oldData = GetOldRawData(gamepadInfo);
        int buttonIndex = mapping.GetButtonIndex(button);
        if (0 <= buttonIndex && buttonIndex < newData.buttons.Length)
        {
            bool wasDown = oldData.buttons[buttonIndex];
            bool isDown  = newData.buttons[buttonIndex];
            return !wasDown && isDown;
        }
        else
        {
            bool wasDown = TryObtainButtonFromAxisValue(button, mapping, oldData);
            bool isDown  = TryObtainButtonFromAxisValue(button, mapping, newData);
            return !wasDown && isDown;
        }
    }

    // PURPOSE: Returns true when the button was released this frame
    public static bool GetButtonUp(GamepadButton button, GamepadInfo gamepadInfo)
    {
        if (!IsValidGamepadId(gamepadInfo.id)) return false;
        CheckPreUpdate();

        GamepadMapping mapping = GetMapping(gamepadInfo, CurrentPlatform);
        GamepadRawData newData = GetRawData(gamepadInfo);
        GamepadRawData oldData = GetOldRawData(gamepadInfo);
        int buttonIndex = mapping.GetButtonIndex(button);
        if (0 <= buttonIndex && buttonIndex < newData.buttons.Length)
        {
            bool wasDown = oldData.buttons[buttonIndex];
            bool isDown  = newData.buttons[buttonIndex];
            return wasDown && !isDown;
        }
        else
        {
            bool wasDown = TryObtainButtonFromAxisValue(button, mapping, oldData);
            bool isDown  = TryObtainButtonFromAxisValue(button, mapping, newData);
            return wasDown && !isDown;
        }
    }

    // PURPOSE: Returns the value of a given axis
    // NOTE: range [-1, 1] for LeftX/Y, RightX/Y, DPadX/Y, range [0, 1] for Left/RightTrigger
    public static float GetAxis(GamepadAxis axis, GamepadInfo gamepadInfo)
    {
        if (!IsValidGamepadId(gamepadInfo.id)) return 0f;
        CheckPreUpdate();

        GamepadMapping mapping = GetMapping(gamepadInfo, CurrentPlatform);
        GamepadRawData data = GetRawData(gamepadInfo);
        int axisIndex = mapping.GetAxisIndex(axis);
        if (0 <= axisIndex && axisIndex < data.axes.Length)
        {
            float axisRaw = data.axes[axisIndex];
            Vector2 axisRange = mapping.GetAxisRange(axis);
            float axisRemapped = Remap(axisRaw, axisRange, GetOutputRange(axis));
            return axisRemapped;
        }
        else
        {
            return TryObtainAxisValueFromButtons(axis, mapping, data);
        }
    }

    // PURPOSE: Returns the value of a given stick
    // NOTE: values in the range [-1, 1]
    public static Vector2 GetStick(GamepadStick stick, GamepadInfo gamepadInfo)
    {
        if (!IsValidGamepadId(gamepadInfo.id)) return Vector2.zero;
        CheckPreUpdate();

        int stickIndex = (int)stick;
        float axisX = GetAxis((GamepadAxis)(stickIndex * 2), gamepadInfo);
        float axisY = GetAxis((GamepadAxis)(stickIndex * 2 + 1), gamepadInfo);
        // NOTE: Axis are already remapped to the correct [-1, 1] range
        return new Vector2(axisX, axisY);
    }
    #endregion

    private static void CheckPreUpdate()
    {
        if (!updatedThisFrame) Instance.PreUpdate();
    }

    [ContextMenu("Apply Mappings")]
    private static void ApplyMappings()
    {
        Instance.mappings = ParseMappings(instance.mappingsData);
    }

    // queries
    private static GamepadRawData[] ReadAllRawData()
    {
        GamepadRawData[] gamepadRawData = new GamepadRawData[NUM_GAMEPADS];
        for (int i = MIN_GAMEPAD_ID; i <= MAX_GAMEPAD_ID; i++)
        {
            gamepadRawData[i] = ReadRawdata(i);
        }
        return gamepadRawData;
    }

    private static GamepadRawData ReadRawdata(int gamepadId)
    {
        Debug.Assert(IsValidGamepadId(gamepadId), $"Invalid gamepadId: {gamepadId}");
        GamepadRawData result = new GamepadRawData();
        result.id = gamepadId;

        result.buttons = new bool[NUM_RAW_BUTTONS];
        for (int buttonIndex = MIN_RAW_BUTTON_ID; buttonIndex <= MAX_RAW_BUTTON_ID; buttonIndex++)
        {
            KeyCode keycode = (KeyCode)(JOYSTICK_BUTTON_BASE + JOYSTICK_BUTTON_DELTA * (gamepadId - 1) + buttonIndex);
            result.buttons[buttonIndex] = Input.GetKey(keycode);
        }

        result.axes = new float[NUM_RAW_AXES];
        for (int axisIndex = MIN_RAW_AXIS_ID; axisIndex <= MAX_RAW_AXIS_ID; axisIndex++)
        {
            result.axes[axisIndex] = Input.GetAxisRaw($"Joystick{gamepadId}_Axis{axisIndex}");
        }
        return result;
    }

    private static GamepadRawData GetRawData(GamepadInfo gamepadInfo)
    {
        return Instance.gamepadRawData[gamepadInfo.id];
    }
    private static GamepadRawData GetOldRawData(GamepadInfo gamepadInfo)
    {
        return Instance.oldGamepadRawData[gamepadInfo.id];
    }
    private static GamepadMapping GetMapping(GamepadInfo gamepadInfo, GamepadPlatform platform)
    {
        return Instance.mappings[(int)platform].data[(int)gamepadInfo.type];
    }

    private static PlatformMappings[] ParseMappings(TextAsset textAsset)
    {
        // TODO: Parse it out as XML
        const int NUM_ROWS = NUM_MAPPED_AXES * 2 + NUM_MAPPED_BUTTONS;
        const int NUM_COLS = NUM_GAMEPAD_TYPES * NUM_PLATFORMS;

        // read the CSV
        string content = textAsset.text;
        string[] lines = content.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        Debug.Assert(lines.Length == NUM_ROWS, $"Incorrect number of rows (want {NUM_ROWS}, have {lines.Length})");
        string[][] cells = new string[lines.Length][];
        for (int i = 0; i < lines.Length; i++)
        {
            cells[i] = lines[i].Split(new char[] { '\t' }, System.StringSplitOptions.None);
            Debug.Assert(cells[i].Length == NUM_COLS, $"Incorrect number of columns (want {NUM_COLS}, have {cells[i].Length}), line {i}");
        }

        Dictionary<string, Vector2> rangeDic = new Dictionary<string, Vector2>
        {
            { "-1_0", V_10 },
            { "-1_1", V_11 },
            { "0_-1", V0_1 },
            { "0_1", V01 },
            { "1_-1", V1_1 },
            { "1_0", V10 }
        };

        PlatformMappings[] mappings = new PlatformMappings[NUM_PLATFORMS];
        for (int platformIndex = 0; platformIndex < NUM_PLATFORMS; platformIndex++)
        {
            string platformName = ((GamepadPlatform)platformIndex).ToString();
            mappings[platformIndex] = new PlatformMappings(platformName);
            mappings[platformIndex].data = new GamepadMapping[NUM_GAMEPAD_TYPES];

            for (int gamepadTypeIndex = 0; gamepadTypeIndex < NUM_GAMEPAD_TYPES; gamepadTypeIndex++)
            {
                string name = ((GamepadType)gamepadTypeIndex).ToString();
                int[] axisIndex = new int[NUM_MAPPED_AXES];
                Vector2[] axisRange = new Vector2[NUM_MAPPED_AXES];
                int[] buttonIndex = new int[NUM_MAPPED_BUTTONS];

                int column = gamepadTypeIndex * NUM_PLATFORMS + platformIndex;

                for (int i = 0; i < NUM_MAPPED_AXES; i++)
                {
                    if (int.TryParse(cells[i][column], out int result))
                    {
                        axisIndex[i] = result;
                    }
                    else axisIndex[i] = -1;
                }
                for (int i = 0; i < NUM_MAPPED_AXES; i++)
                {
                    string key = cells[i + NUM_MAPPED_AXES][column];
                    key = key.Trim();
                    if (rangeDic.ContainsKey(key))
                    {
                        axisRange[i] = rangeDic[key];
                    }
                    else axisRange[i] = V_invalid; // invalid
                }
                for (int i = 0; i < NUM_MAPPED_BUTTONS; i++)
                {
                    if (int.TryParse(cells[i + 2 * NUM_MAPPED_AXES][column], out int result))
                    {
                        buttonIndex[i] = result;
                    }
                    else buttonIndex[i] = -1;
                }

                mappings[platformIndex].data[gamepadTypeIndex] = new GamepadMapping(name, axisIndex, axisRange, buttonIndex);
            }
        }
        return mappings;
    }


    private static GamepadPlatform CurrentPlatform { get { return Instance.currentPlatform; } }

    private static float Remap(float value, Vector2 from, Vector2 to)
    {
        return Remap(value, new Vector4(from.x, from.y, to.x, to.y));
    }
    private static float Remap(float value, Vector4 range)
    {
        float fromMin = range.x;
        float fromMax = range.y;
        float toMin = range.z;
        float toMax = range.w;
        return toMin + (toMax - toMin) * ((value - fromMin) / (fromMax - fromMin));
    }

    private static Vector2 GetOutputRange(GamepadAxis axis)
    {
        if ((int)axis < 6) return new Vector2(-1f, 1f); // sticks, dpad
        else return new Vector2(0f, 1f); // triggers
    }

    private static bool TryObtainButtonFromAxisValue(GamepadButton button, GamepadMapping mapping, GamepadRawData data)
    {
        GamepadAxis axis = AxisFromButton(button);
        int axisIndex = mapping.GetAxisIndex(axis);
        if (0 <= axisIndex && axisIndex < data.axes.Length)
        {
            float axisRaw = data.axes[axisIndex];
            Vector2 axisRange = mapping.GetAxisRange(axis);
            float axisRemapped = Remap(axisRaw, axisRange, GetOutputRange(axis));

            int sign = SignFromButton(button);
            return axisRemapped * sign >= axisPressThreshold;
        }
        return false;
    }

    private static float TryObtainAxisValueFromButtons(GamepadAxis axis, GamepadMapping mapping, GamepadRawData data)
    {
        GamepadButton negativeButton = NegativeButtonFromAxis(axis);
        GamepadButton positiveButton = PositiveButtonFromAxis(axis);

        bool negativePressed = false;
        int negativeButtonIndex = mapping.GetButtonIndex(negativeButton);
        if (0 <= negativeButtonIndex && negativeButtonIndex < data.buttons.Length)
        {
            negativePressed = data.buttons[negativeButtonIndex];
        }
        bool positivePressed = false;
        int positiveButtonIndex = mapping.GetButtonIndex(positiveButton);
        if (0 <= positiveButtonIndex && positiveButtonIndex < data.buttons.Length)
        {
            positivePressed = data.buttons[positiveButtonIndex];
        }

        float value = 0f;
        if (negativePressed) value -= 1f;
        if (positivePressed) value += 1f;
        return value;
    }

    private static GamepadAxis AxisFromButton(GamepadButton button)
    {
        switch (button)
        {
            case  GamepadButton.LeftTrigger:      return GamepadAxis.LeftTrigger;
            case  GamepadButton.RightTrigger:     return GamepadAxis.RightTrigger;
                                              
            case  GamepadButton.DPad_Up:          return GamepadAxis.DPadY;
            case  GamepadButton.DPad_Down:        return GamepadAxis.DPadY;
            case  GamepadButton.DPad_Left:        return GamepadAxis.DPadX;
            case  GamepadButton.DPad_Right:       return GamepadAxis.DPadX;
                                              
            case  GamepadButton.LeftStick_Up:     return GamepadAxis.LeftY;
            case  GamepadButton.LeftStick_Down:   return GamepadAxis.LeftY;
            case  GamepadButton.LeftStick_Left:   return GamepadAxis.LeftX;
            case  GamepadButton.LeftStick_Right:  return GamepadAxis.LeftX;
             
            case  GamepadButton.RightStick_Up:    return GamepadAxis.RightY;
            case  GamepadButton.RightStick_Down:  return GamepadAxis.RightY;
            case  GamepadButton.RightStick_Left:  return GamepadAxis.RightX;
            case  GamepadButton.RightStick_Right: return GamepadAxis.RightX;
        }
        return GamepadAxis.INVALID;
    }

    private static int SignFromButton(GamepadButton button)
    {
        switch (button)
        {
            case  GamepadButton.LeftTrigger:      return +1;
            case  GamepadButton.RightTrigger:     return +1;
                                                         
            case  GamepadButton.DPad_Up:          return +1;
            case  GamepadButton.DPad_Down:        return -1;
            case  GamepadButton.DPad_Left:        return -1;
            case  GamepadButton.DPad_Right:       return +1;
                                                         
            case  GamepadButton.LeftStick_Up:     return +1;
            case  GamepadButton.LeftStick_Down:   return -1;
            case  GamepadButton.LeftStick_Left:   return -1;
            case  GamepadButton.LeftStick_Right:  return +1;
                                                         
            case  GamepadButton.RightStick_Up:    return +1;
            case  GamepadButton.RightStick_Down:  return -1;
            case  GamepadButton.RightStick_Left:  return -1;
            case  GamepadButton.RightStick_Right: return +1;
        }
        return 0;
    }

    private static GamepadButton NegativeButtonFromAxis(GamepadAxis axis)
    {
        switch (axis)
        {
            case GamepadAxis.LeftTrigger:   return GamepadButton.INVALID;
            case GamepadAxis.RightTrigger:  return GamepadButton.INVALID;
        
            case GamepadAxis.DPadX:         return GamepadButton.DPad_Left;
            case GamepadAxis.DPadY:         return GamepadButton.DPad_Down;
            case GamepadAxis.LeftX:         return GamepadButton.LeftStick_Left;
            case GamepadAxis.LeftY:         return GamepadButton.LeftStick_Down;
            case GamepadAxis.RightX:        return GamepadButton.RightStick_Left;
            case GamepadAxis.RightY:        return GamepadButton.RightStick_Down;
        }

        return GamepadButton.INVALID;
    }

    private static GamepadButton PositiveButtonFromAxis(GamepadAxis axis)
    {
        switch (axis)
        {
            case GamepadAxis.LeftTrigger:  return GamepadButton.LeftTrigger;
            case GamepadAxis.RightTrigger: return GamepadButton.RightTrigger;

            case GamepadAxis.DPadX:        return GamepadButton.DPad_Right;
            case GamepadAxis.DPadY:        return GamepadButton.DPad_Up;
            case GamepadAxis.LeftX:        return GamepadButton.LeftStick_Right;
            case GamepadAxis.LeftY:        return GamepadButton.LeftStick_Up;
            case GamepadAxis.RightX:       return GamepadButton.RightStick_Right;
            case GamepadAxis.RightY:       return GamepadButton.RightStick_Up;
        }

        return GamepadButton.INVALID;
    }

    static bool IsValidGamepadId(int id) { return MIN_GAMEPAD_ID <= id && id <= MAX_GAMEPAD_ID; }

    // data
    const int MIN_GAMEPAD_ID = 1;
    const int MAX_GAMEPAD_ID = 4;
    const int NUM_GAMEPADS    = MAX_GAMEPAD_ID + 1; // 5

    const int MIN_RAW_AXIS_ID = 1;
    const int MAX_RAW_AXIS_ID = 12;
    const int NUM_RAW_AXES    = MAX_RAW_AXIS_ID + 1; // 13

    const int MIN_RAW_BUTTON_ID = 0;
    const int MAX_RAW_BUTTON_ID = 19;
    const int NUM_RAW_BUTTONS = MAX_RAW_BUTTON_ID + 1; // 20

    const int JOYSTICK_BUTTON_BASE  = (int)(KeyCode.Joystick1Button0); // 350
    const int JOYSTICK_BUTTON_DELTA = (int)(KeyCode.Joystick2Button0 - KeyCode.Joystick1Button0); // 20

    const int NUM_GAMEPAD_TYPES = (int)GamepadType.COUNT;
    const int NUM_PLATFORMS     = (int)GamepadPlatform.COUNT;
    const int NUM_STICKS        = (int)GamepadStick.COUNT;
    const int NUM_AXES          = (int)GamepadAxis.COUNT;
    const int NUM_BUTTONS       = (int)GamepadButton.COUNT;

    const int NUM_MAPPED_AXES = NUM_AXES;
    const int NUM_MAPPED_BUTTONS = NUM_BUTTONS - 8;

    // all 6 possible mapping {-1, 0, 1}
    readonly static Vector2 V_10      = new Vector2(-1f, 0f);
    readonly static Vector2 V_11      = new Vector2(-1f, 1f);
    readonly static Vector2 V0_1      = new Vector2(0f, -1f);
    readonly static Vector2 V01       = new Vector2(0f, 1f);
    readonly static Vector2 V1_1      = new Vector2(1f, -1f);
    readonly static Vector2 V10       = new Vector2(1f, 0f);
    readonly static Vector2 V_invalid = new Vector2(0f, 1f);


    // classes
    [System.Serializable]
    class PlatformMappings
    {
        public string name;
        public GamepadMapping[] data;

        public PlatformMappings(string name)
        {
            this.name = name;
        }
    }

    [System.Serializable]
    class GamepadMapping
    {
        public string name;
        public int[] axisIndexes;
        public Vector2[] axisRange;
        public int[] buttonIndexes;

        public GamepadMapping(string name, int[] axisIndexes, Vector2[] axisRange, int[] buttonIndexes)
        {
            Debug.Assert(axisIndexes.Length == axisRange.Length);
            this.name = name;
            this.axisIndexes = axisIndexes;
            this.axisRange = axisRange;
            this.buttonIndexes = buttonIndexes;
        }

        private bool HasButton(GamepadButton button)
        {
            return (int)button < buttonIndexes.Length;
        }
        public int GetButtonIndex(GamepadButton button)
        {
            if (HasButton(button)) return buttonIndexes[(int)button];
            return -1;
        }
        private bool HasAxis(GamepadAxis axis) // also for range
        {
            return (int) axis < axisIndexes.Length;
        }
        public int GetAxisIndex(GamepadAxis axis)
        {
            if (HasAxis(axis)) return axisIndexes[(int)axis];
            return -1;
        }
        public Vector2 GetAxisRange(GamepadAxis axis)
        {
            if (HasAxis(axis)) return axisRange[(int)axis];
            return new Vector2(0f, 1f);
        }
    }

    // PURPOSE: Collects and stores the raw input data from the input system
    [System.Serializable]
    class GamepadRawData
    {
        public int id;
        public bool[] buttons;
        public float[] axes;
    }

    // PURPOSE: Singleton
    private static GamepadInput instance;
    public static GamepadInput Instance
    {
        get
        {
            if (instance == null) instance = FindObjectOfType<GamepadInput>();
            return instance;
        }
    }
}