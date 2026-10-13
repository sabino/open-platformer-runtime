/* Test-only hook for the external, unmodified snes_spc source. */
#pragma once
#ifdef __cplusplus
extern "C" {
#endif
void smw_firmware_write_hook(int clock, int address, int value);
#ifdef __cplusplus
}
#endif
#define SPC_DSP_WRITE_HOOK smw_firmware_write_hook
