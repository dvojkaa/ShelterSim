"""Shared helper for animal_* scripts: wraps Model so all y coordinates are shifted by dy."""
class Sh:
    def __init__(self, p, dy): self.p, self.dy = p, dy
    def _l(self, loc): return (loc[0], loc[1] + self.dy, loc[2])
    def box(self, size, loc=(0,0,0), *a, **k): self.p.box(size, self._l(loc), *a, **k)
    def cyl(self, r, h, loc=(0,0,0), *a, **k): self.p.cyl(r, h, self._l(loc), *a, **k)
    def blob(self, size, loc=(0,0,0), *a, **k): self.p.blob(size, self._l(loc), *a, **k)
    def poly(self, prof, th, loc=(0,0,0), *a, **k): self.p.poly(prof, th, self._l(loc), *a, **k)
    def tube(self, pts, *a, **k): self.p.tube([self._l(q) for q in pts], *a, **k)
class ShModel:
    def __init__(self, m, dy): self.m, self.dy = m, dy
    def part(self, name, pivot=(0,0,0)): return Sh(self.m.part(name, (pivot[0], pivot[1]+self.dy, pivot[2])), self.dy)
