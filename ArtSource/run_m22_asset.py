"""Blender CLI runner with a deterministic explicit exit after completed exports.
Usage: blender -b --factory-startup -noaudio -P ArtSource/run_m22_asset.py -- ArtSource/assets/id.py
Avoids optional audio-backend cleanup blocking an otherwise completed headless export.
"""
import os, runpy, sys, traceback
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0,os.path.join(root,'ArtSource','lib'))
sys.path.insert(0,os.path.join(root,'ArtSource','assets'))
try:
    for path in sys.argv[sys.argv.index('--')+1:]:runpy.run_path(os.path.join(root,path),run_name='__main__')
except BaseException:
    traceback.print_exc();sys.stdout.flush();sys.stderr.flush();os._exit(1)
sys.stdout.flush();sys.stderr.flush();os._exit(0)
