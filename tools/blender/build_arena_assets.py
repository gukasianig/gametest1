"""Local, deterministic production for docs/art/Arena/*_BRIEF.md."""

import argparse
import json
import sys
from pathlib import Path

import bpy

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))


REPO = Path(__file__).resolve().parents[2]
OUTPUT = REPO/'_incoming/arena-assets'
PREVIEWS = REPO/'docs/art/Arena/GreenPack'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--part', choices=('all', 'media', 'models'), default='all')
    parser.add_argument('--minimum', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    OUTPUT.mkdir(parents=True, exist_ok=True)
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    manifest_path = OUTPUT/('manifest_minimum.json' if args.minimum else 'manifest.json')
    manifest = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.view.language = 'en_US'
    bpy.context.preferences.view.use_translate_new_dataname = False
    if args.part in ('all', 'media'):
        from arena_pack_effects import effects
        from arena_pack_ground import ground
        manifest['ground'] = ground(OUTPUT, PREVIEWS, args.minimum)
        manifest['effects'] = effects(OUTPUT, PREVIEWS, args.minimum)
        manifest_path.write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    if args.part in ('all', 'models'):
        from arena_pack_models import build
        manifest['models'] = build(OUTPUT, PREVIEWS, args.minimum)
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    if not args.minimum and all(key in manifest for key in ('ground', 'effects', 'models')):
        from arena_pack_ground import MIN_GROUND
        minimum = {'ground': [manifest['ground'][index] for index in MIN_GROUND],
                   'effects': {'sprites': manifest['effects']['sprites'][:2], 'skies': manifest['effects']['skies'][:1]},
                   'models': [model for model in manifest['models'] if model['minimum']]}
        (OUTPUT/'manifest_minimum.json').write_text(json.dumps(minimum,indent=2),encoding='utf-8')
    print(f'ARENA PACK PASS {args.part}: {manifest_path}', flush=True)


if __name__ == '__main__':
    main()
