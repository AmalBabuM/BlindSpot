"""Serve the release build locally, including Unity's compressed WebGL files."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class WebGLHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith((".br", ".gz")):
            path = path.rsplit(".", 1)[0]
        if path.endswith(".wasm"):
            return "application/wasm"
        if path.endswith(".js"):
            return "application/javascript"
        return super().guess_type(path)

    def end_headers(self):
        path = self.path.split("?", 1)[0]
        if path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        elif path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--directory", default=str(Path(__file__).resolve().parents[1] / "Builds" / "WebGL"))
    args = parser.parse_args()
    server = ThreadingHTTPServer(("127.0.0.1", args.port), partial(WebGLHandler, directory=args.directory))
    print(f"WebGL preview: http://127.0.0.1:{args.port}", flush=True)
    server.serve_forever()
