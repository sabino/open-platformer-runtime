#!/usr/bin/env python3
from __future__ import annotations

import argparse
from http.server import HTTPServer, SimpleHTTPRequestHandler
from pathlib import Path


class CrossOriginIsolatedHandler(SimpleHTTPRequestHandler):
    def end_headers(self) -> None:
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        self.send_header("Access-Control-Allow-Origin", "*")
        super().end_headers()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path("web-export/out"))
    parser.add_argument("--port", type=int, default=8060)
    args = parser.parse_args()

    root = args.root.resolve()
    if not (root / "index.html").exists():
        raise SystemExit(f"missing export at {root}/index.html")

    import os

    os.chdir(root)
    server = HTTPServer(("127.0.0.1", args.port), CrossOriginIsolatedHandler)
    print(f"Serving {root} at http://127.0.0.1:{args.port}")
    server.serve_forever()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
