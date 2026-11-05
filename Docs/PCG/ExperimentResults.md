# PCG Experiment Results — Tactical Outpost (mode PCG)

Dibuat otomatis (F9) 2026-10-30 23:07. Setiap baris membangun arena lengkap (pagar, gerbang, obstacle, NavMesh, validasi jalur) dari prefab existing.

## Eksperimen 1 — Seed berbeda

| Seed | Obstacles | Attempts | Rejected | Layout tries | Repair | Connected | Layout hash | Gates (span) | Rejection detail |
|---:|---:|---:|---:|---:|---:|---|---:|---|---|
| 100 | 34/34 | 72 | 38 | 1 | 0 | yes | 1625180412 | W11 N7 S11 | OutsideArea 4, NearGenerator 6, NearGate 5, NearPlayer 2, Spacing 20, Blocked 1 |
| 200 | 34/34 | 74 | 40 | 1 | 0 | yes | -1250752559 | W2 N5 S8 | OutsideArea 3, NearGenerator 4, NearGate 9, NearPlayer 5, Spacing 19 |
| 300 | 34/34 | 68 | 34 | 1 | 0 | yes | -1215942494 | W11 N7 S2 | OutsideArea 4, NearGenerator 3, NearGate 5, NearPlayer 1, Spacing 21 |

## Eksperimen 2 — Reproducibility (seed 500 × 3)

| Run | Obstacles | Attempts | Rejected | Layout tries | Repair | Connected | Layout hash | Gates (span) | Rejection detail |
|---:|---:|---:|---:|---:|---:|---|---:|---|---|
| 1 | 34/34 | 106 | 72 | 1 | 0 | yes | 1295840214 | W7 N3 S10 | OutsideArea 10, NearGenerator 10, NearGate 11, NearPlayer 2, Spacing 38, Blocked 1 |
| 2 | 34/34 | 106 | 72 | 1 | 0 | yes | 1295840214 | W7 N3 S10 | OutsideArea 10, NearGenerator 10, NearGate 11, NearPlayer 2, Spacing 38, Blocked 1 |
| 3 | 34/34 | 106 | 72 | 1 | 0 | yes | 1295840214 | W7 N3 S10 | OutsideArea 10, NearGenerator 10, NearGate 11, NearPlayer 2, Spacing 38, Blocked 1 |

**Identik**: ketiga run menghasilkan layout hash yang sama.

## Eksperimen 3 — Player safe radius (`playerClearRadius`, seed 500)

| Radius (m) | Obstacles | Attempts | Rejected | Layout tries | Repair | Connected | Layout hash | Gates (span) | Rejection detail |
|---:|---:|---:|---:|---:|---:|---|---:|---|---|
| 3 | 34/34 | 103 | 69 | 1 | 0 | yes | 628644938 | W7 N3 S10 | OutsideArea 9, NearGenerator 10, NearGate 11, NearPlayer 1, Spacing 37, Blocked 1 |
| 5 | 34/34 | 103 | 69 | 1 | 0 | yes | 628644938 | W7 N3 S10 | OutsideArea 9, NearGenerator 10, NearGate 11, NearPlayer 1, Spacing 37, Blocked 1 |
| 8 | 34/34 | 106 | 72 | 1 | 0 | yes | 1295840214 | W7 N3 S10 | OutsideArea 10, NearGenerator 10, NearGate 11, NearPlayer 3, Spacing 37, Blocked 1 |

## Eksperimen 4 — Density (`obstacleGap`, seed 500)

| Gap (m) | Obstacles | Attempts | Rejected | Layout tries | Repair | Connected | Layout hash | Gates (span) | Rejection detail |
|---:|---:|---:|---:|---:|---:|---|---:|---|---|
| 1.0 | 34/34 | 66 | 32 | 1 | 0 | yes | 2017026303 | W7 N3 S10 | OutsideArea 4, NearGenerator 7, NearGate 7, Spacing 13, Blocked 1 |
| 2.5 | 34/34 | 106 | 72 | 1 | 0 | yes | 1295840214 | W7 N3 S10 | OutsideArea 10, NearGenerator 10, NearGate 11, NearPlayer 2, Spacing 38, Blocked 1 |
| 4.0 | 34/34 | 132 | 98 | 1 | 0 | yes | -582743170 | W7 N3 S10 | OutsideArea 10, NearGenerator 13, NearGate 12, NearPlayer 3, Spacing 59, Blocked 1 |

## Eksperimen 5 — Weighted random (10.000 pemilihan)

| Weight A : B : C | A | B | C |
|---|---:|---:|---:|
| 60 : 30 : 10 | 59.8% | 30.1% | 10.1% |
| 33 : 33 : 34 | 32.1% | 33.8% | 34.1% |

## Eksperimen 6 — Enemy selection (weighted + difficulty budget)

Tabel: walker w50 cost 1, runner w25 cost 2, skinless w15 cost 2, bloater w10 cost 3 (wave 2+). Budget per wave: 38 / 58 / 78.

| Seed | Wave | Komposisi | Zombie | Cost / budget |
|---:|---:|---|---:|---|
| 100 | 1 | walker 11, runner 10, skinless 3 | 24 | 37 / 38 |
| 100 | 2 | walker 14, runner 9, skinless 7, bloater 4 | 34 | 58 / 58 |
| 100 | 3 | walker 18, runner 13, skinless 7, bloater 6 | 44 | 76 / 78 |
| 200 | 1 | walker 10, runner 8, skinless 6 | 24 | 38 / 38 |
| 200 | 2 | walker 24, runner 11, skinless 3, bloater 2 | 40 | 58 / 58 |
| 200 | 3 | walker 28, runner 9, skinless 7, bloater 6 | 50 | 78 / 78 |
| 300 | 1 | walker 14, runner 7, skinless 5 | 26 | 38 / 38 |
| 300 | 2 | walker 21, runner 11, skinless 5, bloater 1 | 38 | 56 / 58 |
| 300 | 3 | walker 16, runner 15, skinless 9, bloater 4 | 44 | 76 / 78 |
| 500 | 1 | walker 20, runner 5, skinless 3 | 28 | 36 / 38 |
| 500 | 2 | walker 24, runner 8, skinless 7, bloater 1 | 40 | 57 / 58 |
| 500 | 3 | walker 27, runner 12, skinless 6, bloater 5 | 50 | 78 / 78 |
| 500 | 1 | walker 20, runner 5, skinless 3 | 28 | 36 / 38 |
| 500 | 2 | walker 24, runner 8, skinless 7, bloater 1 | 40 | 57 / 58 |
| 500 | 3 | walker 27, runner 12, skinless 6, bloater 5 | 50 | 78 / 78 |

Seed 500 muncul dua kali untuk uji reproducibility komposisi wave. Distribusi 10.000 pemilihan weighted (tanpa budget, wave 3):

| Enemy | Weight | Expected | Hasil |
|---|---:|---:|---:|
| walker | 50 | 50.0% | 49.4% |
| runner | 25 | 25.0% | 25.2% |
| skinless | 15 | 15.0% | 15.3% |
| bloater | 10 | 10.0% | 10.1% |
