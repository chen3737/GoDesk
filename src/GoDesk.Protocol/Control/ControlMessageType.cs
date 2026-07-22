namespace GoDesk.Protocol.Control;

public enum ControlMessageType : ushort
{
    Hello = 1,
    Challenge = 2,
    Pairing = 3,
    PermissionState = 4,
    Error = 5,
}
