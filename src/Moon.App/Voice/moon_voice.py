# Servicio de voz de MOON: Chatterbox Multilingual en este PC (sin internet).
# MOON lo arranca solo cuando se usa la lectura en voz alta y lo cierra al salir.
#
#   python moon_voice.py <puerto>
#   GET  /health  → "ok" cuando el modelo ya está cargado
#   POST /speak   → {"text": "...", "language": "es", "voice": "C:\\...\\voz.wav", "exaggeration": 0.5, "cfg_weight": 0.5}
#                   devuelve el audio en WAV
#
# Solo escucha en 127.0.0.1 (este PC): nada sale a la red.
import io
import json
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import torch
import torchaudio as ta
from chatterbox.mtl_tts import ChatterboxMultilingualTTS

port = int(sys.argv[1])
parent_pid = int(sys.argv[2]) if len(sys.argv) > 2 else None
device = "cuda" if torch.cuda.is_available() else "cpu"
model = None
lock = threading.Lock()          # la tarjeta gráfica genera de una frase en una
prepared = {"voice": None, "exaggeration": None}


def load():
    global model
    loaded = ChatterboxMultilingualTTS.from_pretrained(device=device)
    # La primera frase siempre es lenta (la tarjeta gráfica "se calienta"): se hace aquí, sin que nadie espere.
    loaded.generate("Hola.", language_id="es")
    model = loaded


def watch_parent():
    # Si MOON se cierra (o se cuelga y se cierra a la fuerza), este servicio también: no deja la tarjeta ocupada.
    import ctypes
    import os
    kernel32 = ctypes.windll.kernel32
    handle = kernel32.OpenProcess(0x00100000, False, parent_pid)  # SYNCHRONIZE
    if handle:
        kernel32.WaitForSingleObject(handle, 0xFFFFFFFF)
    os._exit(0)


def speak(text, language, voice, exaggeration, cfg_weight):
    with lock:
        # Preparar la voz cuesta: solo se hace cuando cambia.
        if voice and (prepared["voice"] != voice or prepared["exaggeration"] != exaggeration):
            model.prepare_conditionals(voice, exaggeration=exaggeration)
            prepared["voice"], prepared["exaggeration"] = voice, exaggeration
        wav = model.generate(text, language_id=language, exaggeration=exaggeration, cfg_weight=cfg_weight)
    buffer = io.BytesIO()
    ta.save(buffer, wav.cpu(), model.sr, format="wav")
    return buffer.getvalue()


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):  # sin mensajes en consola
        pass

    def do_GET(self):
        if self.path == "/health":
            ready = model is not None
            self.send_response(200 if ready else 503)
            self.end_headers()
            self.wfile.write(b"ok" if ready else b"loading")
        else:
            self.send_response(404)
            self.end_headers()

    def do_POST(self):
        if self.path != "/speak" or model is None:
            self.send_response(503 if model is None else 404)
            self.end_headers()
            return
        try:
            body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
            audio = speak(body["text"], body.get("language", "es"), body.get("voice"),
                          float(body.get("exaggeration", 0.5)), float(body.get("cfg_weight", 0.5)))
            self.send_response(200)
            self.send_header("Content-Type", "audio/wav")
            self.send_header("Content-Length", str(len(audio)))
            self.end_headers()
            self.wfile.write(audio)
        except Exception as error:  # la frase falla, pero el servicio sigue
            message = str(error).encode("utf-8")
            self.send_response(500)
            self.end_headers()
            self.wfile.write(message)


server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
threading.Thread(target=load, daemon=True).start()
if parent_pid:
    threading.Thread(target=watch_parent, daemon=True).start()
server.serve_forever()
