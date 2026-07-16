using UnityEngine;

public class MobileUIConnector : MonoBehaviour
{
    public void OnJumpDown()     => PlayerInputHandler.StaticMobileJumpDown();
    public void OnJumpUp()       => PlayerInputHandler.StaticMobileJumpUp();
    public void OnActionPressed() => PlayerInputHandler.StaticTriggerAction();
}
