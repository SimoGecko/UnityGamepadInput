using UnityEngine;

public class GamepadTest : MonoBehaviour
{
    // Info about the gamepad with its index and the type
    public GamepadInfo info = new GamepadInfo(id: 1, type: GamepadType.Xbox360);

    public GamepadButton testButton = GamepadButton.Button_South;
    public GamepadAxis testAxis = GamepadAxis.DPadX;
    public GamepadStick testStick = GamepadStick.LeftStick;

    void Update ()
    {
        // Buttons (pressed, held, released)
        bool buttonPressed  = GamepadInput.GetButtonDown(testButton, info);
        bool buttonHeld     = GamepadInput.GetButton    (testButton, info);
        bool buttonReleased = GamepadInput.GetButtonUp  (testButton, info);
        if (buttonPressed)  Debug.Log("Button Pressed");
        if (buttonHeld)     Debug.Log("Button Held");
        if (buttonReleased) Debug.Log("Button Released");

        // 1D Axis values
        float axisValue = GamepadInput.GetAxis(GamepadAxis.DPadX, info);
        if (axisValue != 0f) Debug.Log("Axis: " + axisValue);

        // 2D Stick values
        Vector2 stickValue = GamepadInput.GetStick(GamepadStick.LeftStick, info);
        if (stickValue != Vector2.zero) Debug.Log("Stick: " + stickValue);
    }
}