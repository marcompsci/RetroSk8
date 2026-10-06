// Retro Sk8 - battery and heat (Phase 23): iOS Low Power Mode and the thermal state, read every few seconds by
// DevicePerformance.cs (via DllImport("__Internal")) so the game can drop to 30 FPS before iOS throttles it.
#import <Foundation/Foundation.h>

extern "C" int RetroSk8_LowPowerMode(void)
{
    return [[NSProcessInfo processInfo] isLowPowerModeEnabled] ? 1 : 0;
}

// 0 nominal, 1 fair, 2 serious, 3 critical.
extern "C" int RetroSk8_ThermalState(void)
{
    switch ([[NSProcessInfo processInfo] thermalState])
    {
        case NSProcessInfoThermalStateNominal: return 0;
        case NSProcessInfoThermalStateFair: return 1;
        case NSProcessInfoThermalStateSerious: return 2;
        case NSProcessInfoThermalStateCritical: return 3;
        default: return 0;
    }
}
