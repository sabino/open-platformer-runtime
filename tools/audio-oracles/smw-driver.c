/* MIT test adapter. Includes the pinned MIT reference only from an ignored
 * checkout. This file and library are never compiled into the Godot game. */
#include "smw_spc_player.c"

/* The comparison observes native driver writes; it deliberately uses no DSP
 * backend and no SPC700 CPU. DSP PCM is checked independently with snes_spc. */
Dsp *dsp_init(uint8_t *ram) { return NULL; }
void dsp_reset(Dsp *dsp) {}
void dsp_cycle(Dsp *dsp) {}
void dsp_write(Dsp *dsp, uint8_t address, uint8_t value) {}

void *smw_oracle_new(const uint8_t *data) {
  SmwSpcPlayer *p = calloc(1, sizeof(SmwSpcPlayer));
  if (!p) return NULL;
  memcpy(p->ram, data, 65536);
  p->reg_write_history = calloc(1, sizeof(DspRegWriteHistory));
  if (!p->reg_write_history) { free(p); return NULL; }
  Spc_Reset(p);
  return p;
}
void smw_oracle_delete(void *ptr) {
  SmwSpcPlayer *p = ptr;
  free(p->reg_write_history); free(p);
}
void smw_oracle_command(void *ptr, int port, uint8_t command) {
  ((SmwSpcPlayer *)ptr)->base.input_ports[port] = command;
}
int smw_oracle_writes(void *ptr, uint8_t *pairs) {
  SmwSpcPlayer *p = ptr;
  int count = p->reg_write_history->count;
  for (int i = 0; i < count; i++) {
    pairs[i*2] = p->reg_write_history->addr[i];
    pairs[i*2+1] = p->reg_write_history->val[i];
  }
  return count;
}
int smw_oracle_tick(void *ptr, uint8_t *pairs) {
  SmwSpcPlayer *p = ptr;
  p->reg_write_history->count = 0;
  Spc_Loop_Part2(p, 1);
  return smw_oracle_writes(ptr, pairs);
}
