"""Original synthesized prototype audio. Existing WAVs are preserved unless --replace is passed."""
import argparse
from array import array
import math
from pathlib import Path
import random
import wave

RATE = 22050
ROOT = Path(__file__).resolve().parents[1] / 'Assets/Art/Audio'

def save(name, samples, music=False, replace=False):
    path = ROOT / ('Music' if music else 'Effects') / (name + '.wav')
    if path.exists() and not replace:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    peak = max(abs(v) for v in samples) or 1
    gain = .7 / max(peak, .7)
    pcm = array('h', (round(max(-1, min(1, v * gain)) * 32767) for v in samples))
    with wave.open(str(path), 'wb') as out:
        out.setnchannels(1); out.setsampwidth(2); out.setframerate(RATE); out.writeframes(pcm.tobytes())
    print(name, round(len(samples) / RATE, 2), 'seconds')

def note(pitch, duration, volume, soft=False):
    frequency = 440 * 2 ** ((pitch - 69) / 12)
    result = array('f')
    for i in range(round(duration * RATE)):
        t = i / RATE
        attack = min(1, t / (.06 if soft else .008))
        release = min(1, max(0, (duration - t) / .12))
        envelope = attack * release * (1 if soft else math.exp(-t * 3.5))
        tone = math.sin(2 * math.pi * frequency * t) + .22 * math.sin(4 * math.pi * frequency * t) + .08 * math.sin(6 * math.pi * frequency * t)
        result.append(tone * envelope * volume)
    return result

def music(name, tempo, transpose, replace):
    beat = 60 / tempo; length = round(32 * beat * RATE)
    samples = array('f', [0]) * length
    def add(sound, start):
        offset = round(start * RATE)
        for i, value in enumerate(sound):
            samples[(offset + i) % length] += value
    chords = [(48, 52, 55), (45, 48, 52), (41, 45, 48), (43, 47, 50)]
    melody = [72, None, 76, 79, 76, 74, 72, None, 69, 72, None, 76, 74, 72, 69, None,
              65, None, 69, 72, 76, 72, 69, None, 67, 71, 74, None, 79, 74, 71, None]
    for section, chord in enumerate(chords):
        for pitch in chord:
            # Wrapped tails and attack/release envelopes make the loop seam continuous.
            pad = note(pitch + transpose, 8 * beat + .3, .018, True)
            add(pad, section * 8 * beat)
        for step in range(8):
            pitch = chord[step % 3] + 12 + transpose
            add(note(pitch, beat * 1.8, .07), (section * 8 + step) * beat)
    for step, pitch in enumerate(melody):
        if pitch is not None:
            add(note(pitch + transpose, beat * 2, .10 if name != 'HomeMusic' else .07), step * beat + beat * .08)
    save(name, samples, True, replace)

def effect(name, replace):
    duration = {'UiClick': .09, 'UiOpen': .22, 'UiClose': .18, 'Hoe': .24, 'Plant': .22,
                'Water': .65, 'Harvest': .40, 'Axe': .27, 'Pickaxe': .32, 'Fertilize': .30, 'Trade': .48}[name]
    samples = array('f', [0]) * round(duration * RATE)
    rng = random.Random(name); smooth_noise = 0
    for i in range(len(samples)):
        t = i / RATE; attack = min(1, t / .009); release = min(1, (duration - t) / .035)
        smooth_noise = .7 * smooth_noise + .3 * rng.uniform(-1, 1)
        noise = smooth_noise * attack * release
        if name in ('Hoe', 'Axe'):
            value = (.38 * math.sin(2 * math.pi * (100 if name == 'Hoe' else 180) * t) + noise * .65) * math.exp(-t * 19)
        elif name == 'Pickaxe':
            value = (.18 * math.sin(2 * math.pi * 1800 * t) + .22 * math.sin(2 * math.pi * 3100 * t) + noise * .32) * math.exp(-t * 16)
        elif name == 'Water':
            value = noise * .6 * math.sin(math.pi * t / duration) + .06 * math.sin(2 * math.pi * (700 + 170 * math.sin(t * 35)) * t) * attack * release
        elif name in ('Plant', 'Fertilize'):
            value = noise * .8 * math.exp(-t * 8) + .08 * math.sin(2 * math.pi * 220 * t) * math.exp(-t * 18)
        else:
            value = 0
        samples[i] = value * attack * release
    tones = {'UiClick': [(79, 0)], 'UiOpen': [(72, 0), (79, .065)], 'UiClose': [(76, 0), (69, .06)],
             'Harvest': [(72, .02), (76, .09), (79, .16)], 'Trade': [(76, 0), (79, .10), (84, .21)]}
    for pitch, start in tones.get(name, []):
        sound = note(pitch, duration - start, .24)
        offset = round(start * RATE)
        for i, value in enumerate(sound):
            if offset + i < len(samples): samples[offset + i] += value
    save(name, samples, False, replace)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--replace', action='store_true'); args = parser.parse_args()
    for name, tempo, transpose in [('MenuMusic', 80, 0), ('IslandMusic', 100, 2), ('HomeMusic', 70, -5)]:
        music(name, tempo, transpose, args.replace)
    for name in ['UiClick', 'UiOpen', 'UiClose', 'Hoe', 'Plant', 'Water', 'Harvest', 'Axe', 'Pickaxe', 'Fertilize', 'Trade']:
        effect(name, args.replace)
