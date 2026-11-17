# Final Project Experiment Results — Tactical Outpost (MODE 04)

Dibuat otomatis (F9) 2026-11-14 22:30. Seed sesi 7251654. Profil player sintetis per jendela 60 detik.

## Eksperimen 1 — Respon DDA terhadap tipe player (start difficulty 0.50)

| Persona | Akurasi | Damage/menit | Generator hilang/menit | Kill/menit | Performance | D eval 1 | D eval 2 | D eval 3 | D eval 4 | Label akhir |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Weak | 15% | 80 | 220 | 6 | 0.06 | 0.30 | 0.13 | 0.09 | 0.07 | RECRUIT |
| Average | 35% | 35 | 80 | 12 | 0.47 | 0.48 | 0.48 | 0.47 | 0.47 | NORMAL |
| Strong | 60% | 8 | 15 | 22 | 0.94 | 0.70 | 0.87 | 0.91 | 0.93 | VETERAN |

## Eksperimen 2 — Parameter gameplay hasil DDA

| Difficulty | Label | Zombie wave 1/2/3 | Spawn interval | HP zombie | Speed zombie | Drop chance | Walker : Runner : Skinless : Bloater | Ally support / repair / intercept |
|---:|---|---|---:|---:|---:|---:|---|---|
| 0.00 | RECRUIT | 18 / 28 / 36 | 0.60 s | ×0.85 | ×0.90 | 40% | 60% : 18% : 12% : 10% | 1.35 / 1.25 / 0.55 |
| 0.25 | EASY | 22 / 32 / 42 | 0.53 s | ×0.94 | ×0.95 | 35% | 49% : 21% : 17% : 13% | 1.18 / 1.08 / 0.73 |
| 0.50 | NORMAL | 24 / 36 / 50 | 0.45 s | ×1.03 | ×1.00 | 29% | 38% : 25% : 22% : 15% | 1.00 / 0.90 / 0.90 |
| 0.75 | HARD | 28 / 42 / 56 | 0.38 s | ×1.11 | ×1.05 | 24% | 29% : 27% : 26% : 18% | 0.83 / 0.73 / 1.08 |
| 1.00 | VETERAN | 32 / 46 / 62 | 0.30 s | ×1.20 | ×1.10 | 18% | 20% : 30% : 30% : 20% | 0.65 / 0.55 / 1.25 |

## Eksperimen 3 — Player berubah mendadak (Strong 3× lalu Weak 3×), pengaruh smoothing

| Smoothing | E1 | E2 | E3 | E4 | E5 | E6 |
|---:|---:|---:|---:|---:|---:|---|
| 0.30 | 0.63 | 0.73 | 0.79 | 0.59 | 0.42 | 0.31 |
| 0.60 | 0.70 | 0.87 | 0.91 | 0.71 | 0.51 | 0.31 |
| 1.00 | 0.70 | 0.90 | 0.94 | 0.74 | 0.54 | 0.34 |

maxStep 0.20 membatasi lompatan difficulty per wave, jadi perubahan terasa bertahap.

## Eksperimen 4 — Distribusi event PCG wave 2 (1000 seed, previous = Clear Night)

| Difficulty | Budget | CLEAR NIGHT | SUPPLY DROP | DENSE FOG | BLACKOUT | FRENZY | HORDE RUSH | +Supply assist | Rejected |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 0.2 | 1.7 | 37.1% | 27.4% | 21.8% | 13.7% | 0.0% | 0.0% | 72.6% | Budget 163 |
| 0.5 | 2.3 | 26.9% | 18.1% | 21.0% | 18.4% | 15.6% | 0.0% | 0.0% | Budget 119 |
| 0.8 | 2.9 | 15.6% | 9.4% | 18.7% | 21.2% | 19.5% | 15.6% | 0.0% |  |

## Eksperimen 5 — Reproducibility event (seed 500, difficulty 0.5 → 0.6 → 0.7)

| Run | Wave 1 | Wave 2 | Wave 3 |
|---:|---|---|---|
| 1 | DENSE FOG | SUPPLY DROP | FRENZY |
| 2 | DENSE FOG | SUPPLY DROP | FRENZY |
| 3 | DENSE FOG | SUPPLY DROP | FRENZY |

**Identik**: seed + difficulty sama menghasilkan event yang sama.

## Eksperimen 6 — PCG titik taktis sekutu di map statis

| Seed | Kandidat | Valid | Terpilih | Skor rata-rata | Lihat generator | Rejected |
|---:|---:|---:|---:|---:|---:|---|
| 100 | 160 | 76 | 8 | 0.55 | 7 | NoPath 49, NearAttackPoint 29, NotNavMesh 6, Spacing 23 |
| 200 | 160 | 68 | 8 | 0.53 | 6 | NoPath 55, NearAttackPoint 30, NotNavMesh 7, Spacing 24 |
| 300 | 160 | 80 | 8 | 0.53 | 5 | NoPath 47, NearAttackPoint 26, NotNavMesh 7, Spacing 25 |
| 7251654 | 160 | 86 | 8 | 0.56 | 6 | NotNavMesh 9, NoPath 42, NearAttackPoint 23, Spacing 30 |

## Sesi yang sedang dimainkan

- W1  D 0.50 NORMAL  DENSE FOG  zombies 24
- W2  D 0.70 HARD  HORDE RUSH  zombies 54
- W3  D 0.79 HARD  BLACKOUT  zombies 56
- Ally VIPER: kills 1, generator repaired 0 HP, action now DEFEND

