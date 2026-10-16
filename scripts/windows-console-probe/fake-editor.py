"""A stand-in for $EDITOR: writes an intent into the file it is given and returns.

The console hands the editor a file and reads it back afterwards. This one needs
no terminal, so it shows whether the handoff itself works on Windows, apart
from whether a full-screen editor can draw there.
"""
import sys

with open(sys.argv[-1], "a", encoding="utf-8") as f:
    f.write("Find out why login times out on the staging site.\n")
