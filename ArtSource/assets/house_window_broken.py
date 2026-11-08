import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
import importlib.util
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'house_window.py')).read().replace("build('house_window', False)", "build('house_window_broken', True)")
exec(compile(src, 'house_window.py', 'exec'))
