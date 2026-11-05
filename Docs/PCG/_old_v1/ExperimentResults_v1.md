# PCG Experiment Results — Tactical Outpost

Dibuat otomatis oleh `PcgExperimentRunner` (F9 pada Debug Mode) pada 2026-10-29 08:27.
Semua baris merencanakan **Wave 3** (48 zombie biasa + 3 boss) pada map dan NavMesh asli. Kolom *Rejected* = kandidat yang gagal validasi; *Fallback* = zombie yang memakai titik gerbang tetap karena tidak menemukan posisi valid dalam `maxAttemptsPerObject`.

## Eksperimen 1 — Seed berbeda

| Seed | Spawned | Fallback | Attempts | Rejected | Heavy lane | Boss order | Layout hash | Rejection detail |
|---:|---:|---:|---:|---:|---|---|---:|---|
| 100 | 51 | 3 | 281 | 233 | North | West > South > North | -1093330485 | OffNavMesh 30, Blocked 7, Spacing 194, NearGenerator 2 |
| 200 | 51 | 3 | 305 | 257 | West | South > West > North | -286871794 | OffNavMesh 30, Blocked 7, Spacing 206, NearGenerator 10, NoPath 4 |
| 300 | 51 | 8 | 525 | 482 | South | South > West > North | 1488945368 | OffNavMesh 104, Blocked 15, Spacing 311, NearGenerator 47, NoPath 5 |

## Eksperimen 2 — Reproducibility (seed 500 × 3)

| Run | Spawned | Fallback | Attempts | Rejected | Heavy lane | Boss order | Layout hash | Rejection detail |
|---:|---:|---:|---:|---:|---|---|---:|---|
| 1 | 51 | 5 | 374 | 328 | South | North > West > South | -1268661102 | OffNavMesh 72, Blocked 11, Spacing 212, NearGenerator 30, NoPath 3 |
| 2 | 51 | 5 | 374 | 328 | South | North > West > South | -1268661102 | OffNavMesh 72, Blocked 11, Spacing 212, NearGenerator 30, NoPath 3 |
| 3 | 51 | 5 | 374 | 328 | South | North > West > South | -1268661102 | OffNavMesh 72, Blocked 11, Spacing 212, NearGenerator 30, NoPath 3 |

**Hasil: identik** — ketiga run menghasilkan layout hash yang sama.

## Eksperimen 3 — Player safe radius (`minDistanceFromPlayer`)

Player disimulasikan berdiri di tengah zona lane **North** supaya radius benar-benar berpengaruh. Seed 500.

| Safe radius (m) | Spawned | Fallback | Attempts | Rejected | Heavy lane | Boss order | Layout hash | Rejection detail |
|---:|---:|---:|---:|---:|---|---|---:|---|
| 3 | 51 | 6 | 413 | 368 | South | South > West > North | -1313867356 | OffNavMesh 64, Blocked 12, Spacing 248, NearGenerator 28, NearPlayer 12, NoPath 4 |
| 5 | 51 | 8 | 562 | 519 | South | North > West > South | 1312291551 | OffNavMesh 76, Blocked 15, Spacing 311, NearGenerator 37, NearPlayer 76, NoPath 4 |
| 8 | 51 | 17 | 832 | 798 | South | South > North > West | -1794843056 | OffNavMesh 70, Blocked 11, Spacing 274, NearGenerator 22, NearPlayer 417, NoPath 4 |

## Eksperimen 4 — Density (`minimumSpacing`)

| Spacing (m) | Spawned | Fallback | Attempts | Rejected | Heavy lane | Boss order | Layout hash | Rejection detail |
|---:|---:|---:|---:|---:|---|---|---:|---|
| 0.5 | 51 | 0 | 75 | 24 | South | South > North > West | 1661640004 | OffNavMesh 8, Spacing 8, NearGenerator 5, NoPath 3 |
| 1.5 | 51 | 9 | 499 | 457 | South | North > South > West | -841102112 | OffNavMesh 91, Blocked 11, Spacing 311, NearGenerator 40, NoPath 4 |
| 3.0 | 51 | 28 | 1248 | 1225 | South | West > South > North | -1524479815 | OffNavMesh 186, Blocked 12, Spacing 948, NearGenerator 70, NoPath 9 |

## Eksperimen 5 — Weighted random (10.000 pemilihan, seed 500)

| Weight A : B : C | Hasil A | Hasil B | Hasil C |
|---|---:|---:|---:|
| 60 : 30 : 10 | 59.8% | 30.1% | 10.1% |
| 33 : 33 : 34 | 32.1% | 33.8% | 34.1% |

## Tambahan — Komposisi zombie per lane (seed 500, Wave 3)

| Lane | Komposisi |
|---|---|
| North | bloater 1, runner 7, skinless 3, walker 1 |
| South (heavy) | bloater 9, runner 3, skinless 6, walker 6 |
| West | runner 3, skinless 2, walker 7 |

## Layout match saat eksperimen dijalankan

- Seed: 641186468
- Cover: 10/10 (attempts 24; ditolak: Blocked 8, Spacing 4, Slope 2)
- Dressing: 30/30 (ditolak: OutsideArea 2, OffNavMesh 5, Blocked 1, Spacing 1)
- Supply terakhir: 3/3 (ditolak: OffNavMesh 8, Blocked 1, Spacing 1, NearGenerator 1, NearPlayer 1, NearLane 7, NoPath 9)
