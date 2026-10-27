# Prueba S5: muestras de Chatterbox Multilingual (GPU) con voz por defecto y voces de LibriVox.
import glob, os, time
import librosa, soundfile as sf, torch, torchaudio as ta
from chatterbox.mtl_tts import ChatterboxMultilingualTTS

base = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(base, "..", "muestras")
es = open(os.path.join(base, "..", "texto-es.txt"), encoding="utf-8").read()
en = open(os.path.join(base, "..", "texto-en.txt"), encoding="utf-8").read()

# Recortes de 12 s (saltando la presentación de LibriVox)
refs = []
for mp3 in sorted(glob.glob(os.path.join(base, "refs", "*.mp3"))):
    y, sr = librosa.load(mp3, sr=24000, mono=True, offset=45, duration=12)
    wav = mp3[:-4] + "-ref.wav"
    sf.write(wav, y, sr)
    refs.append(wav)

t = time.time()
model = ChatterboxMultilingualTTS.from_pretrained(device="cuda")
print(f"modelo cargado en {time.time()-t:.1f} s")

def gen(name, text, lang, ref=None):
    t = time.time()
    wav = model.generate(text, language_id=lang, audio_prompt_path=ref)
    dur = wav.shape[-1] / model.sr
    ta.save(os.path.join(out, f"chatterbox-{name}.wav"), wav, model.sr)
    print(f"{name}: {dur:.1f} s de audio en {time.time()-t:.1f} s")

gen("es-por-defecto", es, "es")
gen("en-por-defecto", en, "en")
for i, r in enumerate(refs, 1):
    gen(f"es-voz{i}", es, "es", r)
print(f"VRAM máx: {torch.cuda.max_memory_allocated()/2**30:.1f} GB")
