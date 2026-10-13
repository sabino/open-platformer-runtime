# Third-party source notices

## ares S-DSP signal processing (ISC)

The managed `src/SmwAudioCore/SnesDsp*.cs` pipeline translates the S-DSP
signal-processing routines in `ares/sfc/dsp` at revision
`9408cb43d4948fc3ea6e152a307a34348df3fe04` of
[ares](https://github.com/ares-emulator/ares). No CPU interpreter, platform
backend, or native binary from ares is included. The Gaussian coefficient
table also references the MIT source acknowledged below.

Copyright (c) 2004-2025 ares team, Near et al

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.
THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

## snes_spc validation oracle

Blargg's LGPL-2.1-or-later `snes_spc` is downloaded and built only as an
external local test oracle under ignored `.godot_local/`. Its source and
binary are not linked into, translated into, or distributed with the game.
See `tools/check-native-audio.py` for its pinned revision and usage.

## snesrev/smw

Native ROM data decoding and subsequent verified routine translations reference
the MIT-licensed [snesrev/smw](https://github.com/snesrev/smw) project. Relevant
files and pinned revisions are identified in source comments and reference docs.
ROMs and extracted game content are not included in this repository.

MIT License

Copyright (c) 2023 snesrev
Copyright (c) 2021 elzo_d

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
