# Prueba S5b: clonar la voz de "VOZ PARA LIBROS.mp3" con 3 fragmentos y 2 estilos.
import os, sys, time
import librosa, soundfile as sf, torchaudio as ta
from chatterbox.mtl_tts import ChatterboxMultilingualTTS

src = sys.argv[1]
base = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(base, "..", "muestras", "voz-para-libros")
os.makedirs(out, exist_ok=True)
es = open(os.path.join(base, "..", "texto-es.txt"), encoding="utf-8").read()

refs = []
for i, start in enumerate([15, 90, 170], 1):
    y, sr = librosa.load(src, sr=24000, mono=True, offset=start, duration=14)
    path = os.path.join(base, "refs", f"libros-frag{i}.wav")
    sf.write(path, y, sr)
    sf.write(os.path.join(out, f"frag{i}-original.wav"), y, sr)
    refs.append(path)

model = ChatterboxMultilingualTTS.from_pretrained(device="cuda")
styles = {"natural": dict(exaggeration=0.5, cfg_weight=0.5),
          "pausado": dict(exaggeration=0.35, cfg_weight=0.3)}
for i, ref in enumerate(refs, 1):
    for name, kw in styles.items():
        t = time.time()
        wav = model.generate(es, language_id="es", audio_prompt_path=ref, **kw)
        ta.save(os.path.join(out, f"frag{i}-{name}.wav"), wav, model.sr)
        print(f"frag{i}-{name}: {wav.shape[-1]/model.sr:.1f} s de audio en {time.time()-t:.1f} s")
