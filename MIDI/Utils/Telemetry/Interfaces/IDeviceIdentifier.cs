namespace MIDI.Utils.Telemetry.Interfaces
{
    public interface IDeviceIdentifier
    {
        string GetClientId();
        string GetOsVersion();
        string GetAppVersion();
    }
}