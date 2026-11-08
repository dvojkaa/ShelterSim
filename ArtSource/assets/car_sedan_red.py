import os, sys
import bpy
sys.path.insert(0, os.path.dirname(os.path.abspath(bpy.data.filepath or __file__)))
from _car_common import build_car
build_car('car_sedan_red', 'car_red', 'sedan')
