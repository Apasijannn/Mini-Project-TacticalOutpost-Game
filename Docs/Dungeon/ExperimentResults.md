# Dungeon Experiment Results — Tactical Outpost (mode Procedural Dungeon, Modul 10)

Dibuat otomatis (F9) 2026-11-05 13:35. Default modul: map 60×40, minLeafSize 12, maxLeafSize 20, minRoomSize 5, roomMargin 2, enemyCount 8, itemCount 5, safeRoomCount 1, goal strategi C. 1 cell = 3.50 m di dunia.

## Tugas Eksperimen — 5 generation (seed berbeda)

| Seed | Rooms | Enemy | Item | Start–Goal Distance (BFS cell) | Valid | Attempt | Repair | Floor | Corridor cell | Layout hash |
|---:|---:|---:|---:|---:|---|---:|---:|---:|---:|---:|
| 101 | 11 | 11 + boss | 5 | 104 | Yes | 1 | 0 | 725 | 93 | -216653659 |
| 202 | 7 | 11 + boss | 5 | 55 | Yes | 1 | 0 | 693 | 50 | 600287580 |
| 303 | 11 | 11 + boss | 5 | 97 | Yes | 1 | 0 | 733 | 81 | -271453120 |
| 404 | 12 | 11 + boss | 5 | 117 | Yes | 1 | 0 | 687 | 87 | -1898961648 |
| 505 | 10 | 11 + boss | 5 | 68 | Yes | 1 | 0 | 797 | 62 | -850165684 |

## Eksperimen 1 — Seed (100–500)

| Seed | Rooms | Ukuran room (min / rata-rata / max cell) | Panjang corridor (cell) | Posisi Goal | Start–Goal BFS |
|---:|---:|---|---:|---|---:|
| 100 | 10 | 40 / 79 / 192 | 70 | (53, 6) | 93 |
| 200 | 8 | 48 / 71 / 119 | 74 | (51, 6) | 84 |
| 300 | 12 | 25 / 49 / 88 | 91 | (53, 19) | 121 |
| 400 | 11 | 25 / 63 / 135 | 74 | (42, 6) | 107 |
| 500 | 12 | 30 / 53 / 156 | 88 | (54, 33) | 97 |

## Uji reproducibility (seed 500 × 3)

Hash: -1501184344, -1501184344, -1501184344 → **identik** (seed sama = dungeon sama).

## Eksperimen 2 — minLeafSize (rata-rata seed 100–500)

| minLeafSize | maxLeafSize | Leaf BSP | Room | Rata-rata luas room (cell) | Floor % | Valid |
|---:|---:|---:|---:|---:|---:|---|
| 8 | 20 | 13.0 | 11.8 | 53.3 | 29.3 | 5/5 |
| 12 | 20 | 10.6 | 10.6 | 63.0 | 30.6 | 5/5 |
| 16 | 24 | 5.2 | 5.2 | 136.4 | 31.3 | 5/5 |

## Eksperimen 3 — roomMargin (rata-rata seed 100–500)

| roomMargin | Room | Jarak room → batas leaf (cell, rata-rata) | Rata-rata luas room | Floor % | Valid |
|---:|---:|---:|---:|---:|---|
| 1 | 10.6 | 1.4 | 79.3 | 37.4 | 5/5 |
| 2 | 10.6 | 2.2 | 63.0 | 30.6 | 5/5 |
| 4 | 5.8 | 4.1 | 45.7 | 13.7 | 5/5 |

## Eksperimen 4 — Ukuran dungeon (seed 500, dibangun ke scene tanpa NavMesh)

| Ukuran | Jumlah tile | Floor tile | Wall terlihat (cell) | GameObject | Waktu generation (ms) | Waktu build (ms) | Ukuran dunia (m) |
|---|---:|---:|---:|---:|---:|---:|---|
| 40 × 30 | 1200 | 313 | 216 | 1479 | 0.9 | 42 | 140 × 105 |
| 60 × 40 | 2400 | 729 | 482 | 3033 | 3.1 | 82 | 210 × 140 |
| 80 × 60 | 4800 | 1707 | 865 | 5917 | 5.1 | 203 | 280 × 210 |

## Eksperimen 5 — Enemy density (seed 500)

| Mode | Map | Floor | Enemy | Floor per enemy | Ditolak (alasan) |
|---|---|---:|---:|---:|---|
| Absolut 5 | 60×40 | 729 | 8 | 91 | NotFloor 10, NearStart 3, SafeRoom 1, Occupied 2 |
| Absolut 10 | 60×40 | 729 | 13 | 56 | NotFloor 58, NearStart 7, SafeRoom 2, GoalArea 2, Occupied 9 |
| Absolut 20 | 60×40 | 729 | 23 | 32 | NotFloor 118, NearStart 10, SafeRoom 4, GoalArea 2, Occupied 25 |
| Density 1/50 floor | 40×30 | 313 | 8 | 39 | NotFloor 24, Occupied 2 |
| Density 1/50 floor | 60×40 | 729 | 17 | 43 | NotFloor 82, NearStart 9, SafeRoom 2, GoalArea 2, Occupied 15 |
| Density 1/50 floor | 80×60 | 1707 | 37 | 46 | NotFloor 85, NearStart 3, Occupied 10 |

## Eksperimen 6 — Strategi Start/Goal (BFS path length, cell)

| Seed | A: First → Last | B: Geometric farthest | C: Path farthest |
|---:|---:|---:|---:|
| 101 | 82 | 82 | 104 |
| 202 | 39 | 55 | 55 |
| 303 | 69 | 69 | 97 |
| 404 | 77 | 77 | 117 |
| 505 | 68 | 68 | 68 |
| **rata-rata** | 67.0 | 70.2 | 88.2 |

## Eksperimen 7 — BSP vs Random Walk (seed 100–500, rata-rata)

| Algoritma | Room | Floor % | Dead-end | Corridor/cave cell | Start–Goal BFS | Valid |
|---|---:|---:|---:|---:|---:|---|
| BSP | 10.6 | 30.6 | 0.0 | 79.4 | 100.4 | 5/5 |
| RandomWalk | 0.0 | 46.2 | 0.0 | 1108.2 | 45.2 | 5/5 |

## Eksperimen 8 — Validasi BFS + repair (corridor sengaja dilewati, seed 100–500)

| skipCorridorChance | Corridor dilewati | Repair | Attempt | Valid | Contoh log |
|---:|---:|---:|---:|---|---|
| 0.0 | 0 | 0 | 5 | 5/5 | - |
| 0.3 | 16 | 4 | 5 | 5/5 | seed 100: attempt 1: VALID | repair corridor 1 |
| 0.6 | 34 | 5 | 5 | 5/5 | seed 100: attempt 1: VALID | repair corridor 1 |

## Pengembangan — Corridor width (seed 100–500, rata-rata)

| corridorWidth | Corridor cell | Floor % | Dead-end | Valid |
|---:|---:|---:|---:|---|
| 1 | 79.4 | 30.6 | 0.0 | 5/5 |
| 2 | 154.4 | 33.7 | 0.0 | 5/5 |

## Eksperimen 9 — Power relay (lock-and-key): jumlah relay vs panjang rute (seed 100–500, rata-rata)

Rute = BFS Start → relay terdekat berikutnya (greedy) → … → Goal. Relay wajib terjangkau tanpa melewati Goal (hard constraint).

| relayCount | Relay ditempatkan | Di luar critical path | Penjaga | Rute langsung Start→Goal | Rute dengan relay | Rasio | Valid |
|---:|---:|---:|---:|---:|---:|---:|---|
| 0 | 0.0 | 0.0 | 0.0 | 100.4 | 100.4 | 1.00× | 5/5 |
| 1 | 1.0 | 1.0 | 1.0 | 100.4 | 121.6 | 1.21× | 5/5 |
| 3 | 3.0 | 1.0 | 3.0 | 100.4 | 148.4 | 1.48× | 5/5 |
| 5 | 5.0 | 2.2 | 5.0 | 100.4 | 218.0 | 2.17× | 5/5 |

## Eksperimen 10 — Clear room (default, relay 3)

| Seed | Room | Combat room (dikunci saat dimasuki) | Pintu (bukaan room) | Zombie di combat room | Zombie di corridor / room lain |
|---:|---:|---:|---:|---:|---:|
| 101 | 11 | 5 | 17 | 7 | 4 |
| 202 | 7 | 4 | 7 | 9 | 2 |
| 303 | 11 | 6 | 14 | 9 | 2 |
| 404 | 12 | 5 | 10 | 6 | 5 |
| 505 | 10 | 6 | 11 | 10 | 1 |

## Dungeon yang sedang dimainkan (seed 919375, BSP)

| Metric | Nilai |
|---|---:|
| Room Count | 7 |
| Floor Count | 621 |
| Wall Count (wall terlihat) | 394 |
| Corridor Length | 82 |
| Start–Goal Distance (BFS / lurus) | 60 / 44.3 |
| Enemy Count (+ boss) | 11 + 1 |
| Item Count | 5 |
| Relay (off-path / penjaga) | 3 (2 / 3) |
| Rute Start → relay → Goal (BFS) | 130 |
| Combat room / pintu | 5 / 13 |
| Floor Percentage | 25.9% |
| GameObject | 2573 |
| NavMesh | OK start → goal 195 m |

Constraint:

- ✅ **hard** — Room minimal dua (7 room)
- ✅ **hard** — Start tersedia di floor ((6, 8))
- ✅ **hard** — Goal tersedia di floor ((45, 29))
- ✅ **hard** — Start–Goal terhubung (BFS) (path 60 cell)
- ✅ **hard** — Semua room terjangkau (0 terputus)
- ✅ **hard** — Enemy/item di floor valid
- ✅ **hard** — Enemy jauh dari Start (≥ 8 cell)
- ✅ **hard** — Semua relay terjangkau tanpa melewati Goal (3 relay, 0 terkunci)
- ✅ soft — Jumlah relay sesuai (3) (3 relay)
- ✅ soft — Relay di luar critical path ≥ 1 (2 relay)
- ✅ soft — Start–Goal cukup jauh (≥ 25) (60 cell)
- ✅ soft — Enemy tidak terlalu padat (≤ 1 / 25 floor) (11 enemy / 621 floor)
- ✅ soft — Item tersebar (spacing ≥ 4) (5 item)
- ✅ soft — Room tidak terlalu kecil (terkecil 42 cell)
- ✅ soft — Tidak terlalu linear (ada cabang/loop) (loop 1, junction 4)

Peta ASCII (`#` wall, `.` floor, `S` start, `G` goal, `E` enemy, `B` boss, `i` item, `R` relay, `o` props):

```text
############################################################
############################################################
############################################################
#####################.....##################################
#####################.....##################################
####......###########.....################.......###########
####..o...###########.....################.....o.###########
####......###########..E..################.o.....###########
####....i.###########.....################.......###########
####..R...###########.....################.......###########
####.....E###########.o...################B..G...###########
####......###########............................###########
####......###########.....################.....E.###########
####..o...###########.....################.......###########
####......###########.....################.......###########
####......###########.....################.......###########
#######.#############E.o..###################.##############
#######.#############.....###################.##############
#######.#############.....###################.##############
#######.#############i....###################.##############
#######.###############.#####################.##############
#######E###############.#####################.##############
#######.###############.#####################.##############
#######.###############.#####################.##############
#######.###############.#####################.##############
##........#############.##......E############.##############
##........####.....####.##.......############.##....E.....##
##........####..o..####.......R.E.........................##
##........####.....#######i......############.##.o......R.##
##........####.....#######...o.o.############.##..........##
##........####....E#######.......############.##E......E..##
##....S.......................###############.............##
##........####.....#############################..........##
##.o......####.....#############################.........i##
##......o.####..o..#############################....o.....##
##........####.....#############################..........##
##........####.....#############################...i......##
##........##################################################
############################################################
############################################################
```
