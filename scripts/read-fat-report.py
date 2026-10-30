"""Read the fixed diagnostic path from an isolated FAT test image, never write it."""
import pathlib
import sys

from pyfatfs.PyFatFS import PyFatFS

image, output = map(pathlib.Path, sys.argv[1:])
with PyFatFS(str(image), read_only=True) as volume:
    data = volume.readbytes('/EFI/BOOT/diagnostic.txt')
if len(data) > 65536:
    raise ValueError('Diagnostic exceeds the test limit')
output.parent.mkdir(parents=True, exist_ok=True)
output.write_bytes(data)
