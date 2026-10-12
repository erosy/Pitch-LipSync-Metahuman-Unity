"""Contact sheets and descriptive pixel statistics for matched Game-view captures."""
from pathlib import Path
from PIL import Image, ImageDraw
import json
import numpy as np

root = Path(__file__).resolve().parents[2] / 'Docs' / 'HairRendering'
names = ['BaselineVerified', 'TaaVeryHigh', 'SmaaMsaa4Verified', 'Subdivision1Verified', 'Smoothness035', 'Smoothness020Verified', 'Width125', 'Scale125']
available = [n for n in names if (root / f'{n}_Front.png').exists()]
sheet = Image.new('RGB', (1320, ((len(available)+2)//3)*430), '#eeeeee')
draw = ImageDraw.Draw(sheet)
stats = []
for i, name in enumerate(available):
    image = Image.open(root / f'{name}_Front.png').convert('RGB')
    crop = image.crop((680, 70, 1120, 470))
    x, y = (i % 3)*440, (i // 3)*430
    sheet.paste(crop, (x, y+30)); draw.text((x+10, y+10), name, fill='black')
    # Fixed inner hair ROI, excludes the face and silhouette. Descriptive, not an acceptance test.
    roi = np.asarray(image.crop((810, 135, 1010, 205)), dtype=float).mean(axis=2)
    stats.append({'variant': name, 'meanHairLuminance': float(roi.mean()), 'meanAdjacentPixelDifference': float(np.abs(np.diff(roi, axis=1)).mean())})
sheet.save(root / 'ComparisonContactSheet.png')
(root / 'PixelStatistics.json').write_text(json.dumps(stats, indent=2))
if (root / 'Comparisons.json').exists():
    results = json.loads((root / 'Comparisons.json').read_text())
    for row in results:
        print(row['variant'], row['pose'], 'GPU', row['gpuMedianMs'], 'CPU', row['cpuMedianMs'], 'points', row['simulationPoints'], 'renderVertices', row['staging'])
print(json.dumps(stats, indent=2))

# Matching front/side views and short recordings sampled from actual Game frames.
before, after = 'BaselineVerified', 'Smoothness020Verified'
if (root / f'{after}_Side.png').exists():
    comparison = Image.new('RGB', (880, 860), '#eeeeee')
    d = ImageDraw.Draw(comparison)
    for column, name in enumerate([before, after]):
        for row, pose in enumerate(['Front', 'Side']):
            frame = Image.open(root / f'{name}_{pose}.png').convert('RGB')
            crop = frame.crop((680, 70, 1120, 470))
            comparison.paste(crop, (column*440, row*430+30))
            d.text((column*440+10, row*430+10), f'{"Before" if column==0 else "After"} - {pose.lower()}', fill='black')
    comparison.save(root / 'BeforeAfter.png')
    frames = []
    for number in range(0, 61, 6):
        canvas = Image.new('RGB', (880, 430), '#eeeeee')
        d = ImageDraw.Draw(canvas)
        for column, name in enumerate([before, after]):
            raw = Image.open(root / f'{name}_Motion_{number:03d}.png').convert('RGB')
            canvas.paste(raw.crop((680, 70, 1120, 470)), (column*440, 30))
            d.text((column*440+10, 10), 'Before' if column==0 else 'After', fill='black')
        frames.append(canvas)
    frames[0].save(root / 'BeforeAfterMotion.gif', save_all=True, append_images=frames[1:], duration=100, loop=0)
