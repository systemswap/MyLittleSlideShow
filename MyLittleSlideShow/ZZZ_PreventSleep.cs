using System;
using System.Runtime.InteropServices;

public class PreventSleep
{
    // Import der SetThreadExecutionState-Methode aus der kernel32.dll
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern uint SetThreadExecutionState(uint esFlags);

    // Konstanten für den Zustand der Threadausführung
    public const uint EXECUTION_STATE_CONTINUOUS = 0x80000000;
    public const uint EXECUTION_STATE_SYSTEM_REQUIRED = 0x00000001;
    public const uint EXECUTION_STATE_DISPLAY_REQUIRED = 0x00000002;
    public const uint EXECUTION_STATE_AWAYMODE_REQUIRED = 0x00000040;

    // Methode zum Festlegen des Threadausführungszustands, um das Schlafen des PCs zu verhindern
    // Diese Methode gibt jetzt einen uint zurück, der dem Rückgabewert von SetThreadExecutionState entspricht
    public uint SetThreadExecutionStateFlags(uint flags)
    {
        return SetThreadExecutionState(flags);
    }

    // Verwendung der Methode
    public void KeepSystemAwake()
    {
        uint previousState = SetThreadExecutionStateFlags(EXECUTION_STATE_CONTINUOUS | EXECUTION_STATE_SYSTEM_REQUIRED | EXECUTION_STATE_DISPLAY_REQUIRED);
        if (previousState == 0)
        {
            throw new InvalidOperationException("SetThreadExecutionState failed. Unable to keep the system awake.");
        }
    }

    // Methode zum Zurücksetzen des Threadausführungszustands
    public void ResetThreadExecutionState()
    {
        SetThreadExecutionStateFlags(EXECUTION_STATE_CONTINUOUS);
    }
}