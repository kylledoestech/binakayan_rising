# Performance (#52)

Proposal Table 5 target: **a steady 30–60 FPS in combat** on the minimum spec (Windows 10 64-bit,
Core i3 or Ryzen 3, 8 GB RAM, integrated graphics).

## How it's measured

`Tools/qa/fps.sh` launches the player on the `bench` shot route with `-brFps`. The route plays q10,
the largest battle (6 Katipuneros against 14 Spanish of all five types), from start to finish at
1× speed. `UI/Shell/QaProbes.cs` records every frame while the battle is on screen, skipping a
60-frame warm-up, and writes the average, the 1% low and the worst frame.

## Results

| Date | Machine | GPU | Resolution | Frames | Avg FPS | 1% low | Worst frame |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2026-09-24 | Ryzen 7 6800H, 16 threads, 28 GB, Linux | Radeon 680M **integrated** (OpenGL) | 1920×1080, vsync on | 11,520 over 115 s | **100.0** | **81.3** | 56 ms |

The target is met with a wide margin on integrated graphics. The average of 100 is the vsync cap
of this panel, so the real headroom is higher. The single worst frame (56 ms) was not traced.

## Still to do on real minimum-spec hardware

The 6800H is well above a Core i3. On a school or lab PC that matches the spec, install the
Windows release and run the same benchmark:

```
"Binakayan Rising.exe" -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -brShot bench -brShotDir out -brSaveDir tmp -brFps fps.txt
```

Then add a row to the table. **Pass** means an average of 30 or more and a 1% low of 25 or more.
