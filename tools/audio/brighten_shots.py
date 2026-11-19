# Пересборка записей выстрелов (Shot / AutoShot / Shotgun) из исходников BigSoundBank с более мягким фильтром.
# Было: «приглушённая обработка B», ФНЧ 2,6 кГц. 09.11.2026 владелец: «очень приглушённо, чуть лучше» -> ФНЧ 5,5 кГц.
# Длина и пиковый уровень берутся у текущих файлов в Assets/Audio/Resources/RecordedAudio, чтобы баланс не сдвинулся.
# Запуск: blender.exe -b --factory-startup -P tools/audio/brighten_shots.py -- [--cutoff 5500] [--analyze]
import sys, os, wave, struct
import aud, numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "_incoming", "audio-recordings-2026-11-09")
DST = os.path.join(ROOT, "Assets", "Audio", "Resources", "RecordedAudio")
MAP = {"Shot": "pistol-357-magnum.ogg", "AutoShot": "auto-beretta-m12.ogg", "Shotgun": "shotgun-winchester-xtr.ogg"}

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
cutoff = float(args[args.index("--cutoff") + 1]) if "--cutoff" in args else 5500.0
analyze = "--analyze" in args

def read_wav(path):
    with wave.open(path, "rb") as w:
        rate, ch, width, n = w.getframerate(), w.getnchannels(), w.getsampwidth(), w.getnframes()
        raw = w.readframes(n)
    data = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0
    return rate, ch, data.reshape(-1, ch)

def write_wav(path, rate, data):
    data = np.clip(data, -1.0, 1.0)
    pcm = (data * 32767.0).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(pcm.shape[1]); w.setsampwidth(2); w.setframerate(rate)
        w.writeframes(pcm.tobytes())

def centroid(mono, rate):
    spec = np.abs(np.fft.rfft(mono * np.hanning(len(mono))))
    freq = np.fft.rfftfreq(len(mono), 1.0 / rate)
    return float((spec * freq).sum() / max(1e-9, spec.sum()))

for key, source in MAP.items():
    cur_path = os.path.join(DST, key + ".wav")
    rate, ch, current = read_wav(cur_path)
    length = current.shape[0]
    peak = float(np.abs(current).max())
    info = "%s: %d Hz, %d ch, %.3f s, peak %.3f, centroid %.0f Hz" % (key, rate, ch, length / rate, peak, centroid(current.mean(axis=1), rate))
    if analyze:
        print(info); continue
    sound = aud.Sound(os.path.join(SRC, source)).resample(rate, False).rechannel(ch).lowpass(cutoff, 0.7)
    data = sound.data()
    if data.ndim == 1: data = data.reshape(-1, 1)
    mono = np.abs(data).max(axis=1)
    onset = int(np.argmax(mono > mono.max() * 0.05))
    onset = max(0, onset - int(rate * 0.002))
    clip = data[onset:onset + length].copy()
    if clip.shape[0] < length: clip = np.vstack([clip, np.zeros((length - clip.shape[0], ch), np.float32)])
    fade = int(rate * 0.04)
    clip[-fade:] *= np.linspace(1.0, 0.0, fade)[:, None]
    clip *= peak / max(1e-6, float(np.abs(clip).max()))
    write_wav(cur_path, rate, clip)
    print(info + "  ->  centroid %.0f Hz (cutoff %.0f)" % (centroid(clip.mean(axis=1), rate), cutoff))
