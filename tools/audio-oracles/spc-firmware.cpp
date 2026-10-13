/* MIT test adapter. The original ROM firmware executes only in this external
 * LGPL oracle library, never in the Godot runtime or managed audio engine. */
#include "SNES_SPC.h"
#include <cstdint>
#include <vector>

struct Event { int64_t clock; int32_t address, value; };
struct Oracle { SNES_SPC spc; int64_t clock = 0; std::vector<Event> writes; };
static Oracle* active;

extern "C" void smw_firmware_write_hook(int clock, int address, int value) {
  if (active) active->writes.push_back({active->clock + clock, address, value});
}
extern "C" void* smw_firmware_new(const unsigned char* snapshot, int size) {
  auto* instance = new Oracle;
  if (instance->spc.init() || instance->spc.load_spc(snapshot, size)) {
    delete instance; return nullptr;
  }
  return instance;
}
extern "C" void smw_firmware_delete(void* ptr) { active = nullptr; delete (Oracle*)ptr; }
extern "C" void smw_firmware_command(void* ptr, int port, int value) {
  active = (Oracle*)ptr; active->spc.write_port(0, port, value);
}
extern "C" const char* smw_firmware_render(void* ptr, int samples, short* pcm) {
  active = (Oracle*)ptr;
  const char* error = active->spc.play(samples, pcm);
  active->clock += (samples / 2) * 32;
  return error;
}
extern "C" int smw_firmware_events(void* ptr, Event* events, int capacity) {
  const auto& list = ((Oracle*)ptr)->writes;
  if (events && capacity >= (int)list.size()) std::copy(list.begin(), list.end(), events);
  return (int)list.size();
}
extern "C" void smw_firmware_snapshot(void* ptr, unsigned char* bytes) {
  SNES_SPC::init_header(bytes); ((Oracle*)ptr)->spc.save_spc(bytes);
}
